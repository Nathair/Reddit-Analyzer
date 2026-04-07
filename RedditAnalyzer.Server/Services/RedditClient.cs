using HtmlAgilityPack;
using PuppeteerSharp;
using RedditAnalyzer.Server.Models;
using System.Text.Json;

namespace RedditAnalyzer.Server.Services
{
    public interface IRedditClient
    {
        Task<List<RedditPost>> GetSubredditHtmlPostsAsync(string subreddit, int limit);
        Task<List<RedditPost>> GetSubredditChromiumPostsAsync(string subreddit, int limit);
        Task<List<RedditPost>> GetSubredditApiPostsAsync(string subreddit, int limit);
        Task<List<string>> AutocompleteSubredditsAsync(string query);
    }

    public class RedditClient : IRedditClient
    {
        private readonly HttpClient _httpClient;
        private readonly IChromiumBrowserManager _browserManager;
        private readonly ILogger<RedditClient> _logger;
        private static readonly SemaphoreSlim _concurrencySemaphore = new(3, 3); // Max 3 parallel pages

        public RedditClient(IHttpClientFactory httpClientFactory, IChromiumBrowserManager browserManager, ILogger<RedditClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("RedditClient");
            _browserManager = browserManager;
            _logger = logger;
        }

        public async Task<List<RedditPost>> GetSubredditChromiumPostsAsync(string subreddit, int limit)
        {
            await _concurrencySemaphore.WaitAsync();
            var posts = new List<RedditPost>();
            try
            {
                var browser = await _browserManager.GetBrowserAsync();
                await using var page = await browser.NewPageAsync();

                var url = $"https://www.reddit.com/{subreddit}/";
                await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

                _logger.LogInformation($"Navigating to {url}...");
                var navigationOptions = new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
                    Timeout = 30000
                };
                await page.GoToAsync(url, navigationOptions);

                int scrollCount = 0;
                int maxScrolls = 10;
                while (scrollCount < maxScrolls)
                {
                    var count = await page.GetCountAsync("shreddit-post");
                    if (count >= limit) break;

                    _logger.LogInformation($"Only {count} posts found. Scrolling down... (attempt {scrollCount + 1})");
                    await page.ScrollDownAsync();
                    await page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions { Timeout = 3000 });
                    scrollCount++;
                }

                await page.WaitForSelectorAsync("shreddit-post", new WaitForSelectorOptions { Timeout = 10000 });
                var postElements = await page.QuerySelectorAllAsync("shreddit-post");

                _logger.LogInformation($"Found {postElements.Length} posts on {subreddit}");

                foreach (var el in postElements.Take(limit))
                {
                    var title = await el.GetAttributeAsync("post-title");
                    var permalink = await el.GetAttributeAsync("permalink");
                    var imageUrl = await el.GetAttributeAsync("content-href");
                    var body = await el.GetChildTextAsync("div[slot=\"text-body\"]");

                    var postUrl = string.IsNullOrEmpty(permalink) ? "" : (permalink.StartsWith("http") ? permalink : "https://www.reddit.com" + permalink);

                    var post = new RedditPost
                    {
                        Title = title ?? "",
                        Body = body ?? "",
                        PostUrl = postUrl
                    };

                    if (IsImageUrl(imageUrl))
                    {
                        post.HasImage = true;
                        post.ImageUrl = imageUrl!;
                    }
                    posts.Add(post);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Chromium parsing error in {subreddit}");
            }
            finally
            {
                _concurrencySemaphore.Release();
            }
            return posts;
        }

        public async Task<List<RedditPost>> GetSubredditApiPostsAsync(string subreddit, int limit)
        {
            var url = $"{subreddit}.json?limit={limit}";
            var response = await _httpClient.GetAsync(url);

            var rawJson = response.IsSuccessStatusCode ?
                await response.Content.ReadAsStringAsync() :
                null;

            return !string.IsNullOrEmpty(rawJson) ?
                ParseJsonPosts(rawJson) :
                new List<RedditPost>();
        }

        public async Task<List<string>> AutocompleteSubredditsAsync(string query)
        {
            var results = new List<string>();
            var url = $"api/subreddit_autocomplete_v2.json?query={query}&include_over_18=false";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return results;

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("children", out var children))
            {
                foreach (var child in children.EnumerateArray())
                {
                    var name = child.GetProperty("data").GetProperty("display_name_prefixed").GetString();
                    if (!string.IsNullOrEmpty(name)) results.Add(name);
                }
            }
            return results;
        }

        public async Task<List<RedditPost>> GetSubredditHtmlPostsAsync(string subreddit, int limit)
        {
            var posts = new List<RedditPost>();
            try
            {
                var entryUrl = $"https://old.reddit.com/{subreddit}/";
                var response = await _httpClient.GetAsync(entryUrl);
                if (!response.IsSuccessStatusCode) return posts;

                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var oldRedditPosts = doc.DocumentNode.SelectNodes("//div[contains(@class, 'thing')]")?.Take(limit);
                if (oldRedditPosts != null)
                {
                    foreach (var el in oldRedditPosts)
                    {
                        var titleEl = el.SelectSingleNode(".//a[contains(@class, 'title')]");
                        var title = titleEl?.InnerText ?? "";
                        var permaLink = el.GetAttributeValue("data-permalink", "");
                        var postUrl = string.IsNullOrEmpty(permaLink) ? "" : "https://www.reddit.com" + permaLink;

                        var bodyEl = el.SelectSingleNode(".//div[contains(@class, 'usertext-body')]//div[contains(@class, 'md')]");
                        var body = bodyEl?.InnerText ?? "";

                        var post = new RedditPost
                        {
                            Title = title,
                            Body = body,
                            PostUrl = postUrl
                        };

                        var thumbnail = el.SelectSingleNode(".//a[contains(@class, 'thumbnail')]")?.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(thumbnail) && thumbnail.EndsWith(".jpeg"))
                        {
                            post.HasImage = true;
                            post.ImageUrl = thumbnail;
                        }

                        posts.Add(post);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"HTML parsing error in {subreddit}");
            }
            return posts;
        }

        private List<RedditPost> ParseJsonPosts(string rawJson)
        {
            var posts = new List<RedditPost>();
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data) && data.TryGetProperty("children", out var children))
                {
                    foreach (var child in children.EnumerateArray())
                    {
                        var postData = child.GetProperty("data");
                        var postModel = new RedditPost
                        {
                            Title = postData.GetProperty("title").GetString() ?? "",
                            Body = postData.TryGetProperty("selftext", out var selftext) ? selftext.GetString() ?? "" : "",
                            PostUrl = postData.TryGetProperty("permalink", out var pL) ? "https://www.reddit.com" + pL.GetString() : ""
                        };

                        var postUrlValue = postData.TryGetProperty("url", out var pUrl) ? pUrl.GetString() ?? "" : "";
                        postModel.HasImage = IsImageUrl(postUrlValue) || (postData.TryGetProperty("post_hint", out var hint) && hint.GetString() == "image");
                        postModel.ImageUrl = postModel.HasImage ? postUrlValue : "";
                        posts.Add(postModel);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Parsing error in JSON data");
            }
            return posts;
        }

        private bool IsImageUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            var lowerUrl = url.ToLower();
            return lowerUrl.EndsWith(".jpg") || lowerUrl.EndsWith(".jpeg") || lowerUrl.EndsWith(".png") || lowerUrl.EndsWith(".gif");
        }
    }

    public static class PuppeteerExtensions
    {
        public static async Task<string> GetAttributeAsync(this IElementHandle el, string name)
        {
            return await el.EvaluateFunctionAsync<string>($"(el, attrName) => el.getAttribute(attrName)", name);
        }

        public static async Task<string> GetChildTextAsync(this IElementHandle el, string selector)
        {
            return await el.EvaluateFunctionAsync<string>($"(el, sel) => {{ const b = el.querySelector(sel); return b ? b.innerText : ''; }}", selector);
        }

        public static async Task<int> GetCountAsync(this IPage page, string selector)
        {
            return await page.EvaluateExpressionAsync<int>($"document.querySelectorAll('{selector}').length");
        }

        public static async Task ScrollDownAsync(this IPage page)
        {
            await page.EvaluateExpressionAsync("window.scrollBy(0, window.innerHeight * 2)");
        }
    }
}

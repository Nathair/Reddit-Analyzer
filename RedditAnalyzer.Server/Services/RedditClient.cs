using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using RedditAnalyzer.Server.Models;

namespace RedditAnalyzer.Server.Services
{
    public interface IRedditClient
    {
        Task<List<RedditPost>> GetSubredditHtmlPostsAsync(string subreddit, int limit);
        Task<List<RedditPost>> GetSubredditApiPostsAsync(string subreddit, int limit);
        Task<List<string>> AutocompleteSubredditsAsync(string query);
    }

    public class RedditClient : IRedditClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RedditClient> _logger;

        public RedditClient(IHttpClientFactory httpClientFactory, ILogger<RedditClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("RedditClient");
            _logger = logger;
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
}

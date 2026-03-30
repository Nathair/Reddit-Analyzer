using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using RedditAnalyzer.Server.Models;

namespace RedditAnalyzer.Server.Services
{
    public interface IRedditService
    {
        Task<RedditAnalysisResult> GetAnalyzedPostsAsync(RedditSearchRequest request, bool verbose = false);
        Task<List<string>> SearchSubredditsAsync(string query);
    }

    public class RedditService : IRedditService
    {
        private readonly IRedditClient _redditClient;
        private readonly IPostMatcher _postMatcher;
        private readonly IMapper _mapper;
        private readonly ILogger<RedditService> _logger;

        public RedditService(IRedditClient redditClient, IPostMatcher postMatcher, IMapper mapper, ILogger<RedditService> logger)
        {
            _redditClient = redditClient;
            _postMatcher = postMatcher;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<List<string>> SearchSubredditsAsync(string query) => 
            await _redditClient.AutocompleteSubredditsAsync(query);

        public async Task<RedditAnalysisResult> GetAnalyzedPostsAsync(RedditSearchRequest request, bool verbose = false)
        {
            var tasks = request.Items.Select(item => ProcessSubredditInternalAsync(item, request.Limit, verbose));
            var processed = await Task.WhenAll(tasks);
            
            var result = new RedditAnalysisResult();
            foreach (var r in processed)
            {
                result.Subreddits[r.Subreddit] = new SubredditAnalysisResult { Posts = r.Posts };
            }
            return result;
        }

        private async Task<(string Subreddit, List<RedditPostDto> Posts)> ProcessSubredditInternalAsync(SubredditItem item, int limit, bool verbose)
        {
            var postsList = new List<RedditPostDto>();
            var patterns = _postMatcher.CreatePatterns(item.Keywords).ToList();
            var rawJson = await _redditClient.GetSubredditRawJsonAsync(item.Subreddit, limit);
            
            if (string.IsNullOrEmpty(rawJson)) return (item.Subreddit, postsList);

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

                        if (_postMatcher.IsMatch(postModel.Title, postModel.Body, patterns))
                        {
                            var dto = _mapper.Map<RedditPostDto>(postModel);
                            
                            if (!verbose)
                            {
                                dto.HasImage = false;
                                dto.ImageUrl = null;
                                dto.PostUrl = null;
                            }
                            
                            postsList.Add(dto);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Parsing error in {item.Subreddit}");
            }

            return (item.Subreddit, postsList);
        }

        private bool IsImageUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            var lowerUrl = url.ToLower();
            return lowerUrl.EndsWith(".jpg") || lowerUrl.EndsWith(".jpeg") || lowerUrl.EndsWith(".png") || lowerUrl.EndsWith(".gif");
        }
    }
}

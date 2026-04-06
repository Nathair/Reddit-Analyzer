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
            var tasks = request.Items.Select(item => ProcessSubredditInternalAsync(item, request.Limit, verbose, request.Mode));
            var processed = await Task.WhenAll(tasks);

            var result = new RedditAnalysisResult();
            foreach (var r in processed)
            {
                result.Subreddits[r.Subreddit] = new SubredditAnalysisResult { Posts = r.Posts };
            }
            return result;
        }

        private async Task<(string Subreddit, List<RedditPostDto> Posts)> ProcessSubredditInternalAsync(SubredditItem item, int limit, bool verbose, FetchMode mode)
        {
            var postsList = new List<RedditPostDto>();
            var patterns = _postMatcher.CreatePatterns(item.Keywords).ToList();

            List<RedditPost> sourcePosts = new();
            sourcePosts = mode == FetchMode.HtmlParsing ?
                await _redditClient.GetSubredditHtmlPostsAsync(item.Subreddit, limit) :
                await _redditClient.GetSubredditApiPostsAsync(item.Subreddit, limit);

            foreach (var postModel in sourcePosts)
            {
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

            return (item.Subreddit, postsList);
        }

    }
}

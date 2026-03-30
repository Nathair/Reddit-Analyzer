using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RedditAnalyzer.Server.Services
{
    public interface IRedditClient
    {
        Task<string?> GetSubredditRawJsonAsync(string subreddit, int limit);
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

        public async Task<string?> GetSubredditRawJsonAsync(string subreddit, int limit)
        {
            var url = $"{subreddit}.json?limit={limit}";
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : null;
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
    }
}

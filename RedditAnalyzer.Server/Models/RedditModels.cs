using System.Collections.Generic;

namespace RedditAnalyzer.Server.Models
{
    public class SubredditItem
    {
        public string Subreddit { get; set; } = string.Empty;
        public string[] Keywords { get; set; } = Array.Empty<string>();
    }

    public class RedditSearchRequest
    {
        public List<SubredditItem> Items { get; set; } = new();
        public int Limit { get; set; } = 25;
    }

    public class RedditPost
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool HasImage { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string PostUrl { get; set; } = string.Empty;
    }

    public class RedditPostDto
    {
        public string Title { get; set; } = string.Empty;
        public bool? HasImage { get; set; }
        public string? ImageUrl { get; set; }
        public string? PostUrl { get; set; }
    }

    public class SubredditAnalysisResult
    {
        public List<RedditPostDto> Posts { get; set; } = new();
        public int Count => Posts.Count;
    }

    public class RedditAnalysisResult
    {
        public Dictionary<string, SubredditAnalysisResult> Subreddits { get; set; } = new();
    }
}

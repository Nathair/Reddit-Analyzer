using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RedditAnalyzer.Server.Services
{
    public interface IPostMatcher
    {
        bool IsMatch(string title, string body, IEnumerable<Regex> patterns);
        IEnumerable<Regex> CreatePatterns(string[] keywords);
    }

    public class PostMatcher : IPostMatcher
    {
        public IEnumerable<Regex> CreatePatterns(string[] keywords)
        {
            if (keywords == null || keywords.Length == 0)
                return Enumerable.Empty<Regex>();

            return keywords.Select(k => new Regex($@"\b{Regex.Escape(k)}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }

        public bool IsMatch(string title, string body, IEnumerable<Regex> patterns)
        {
            // If no patterns, result is a match (accept all)
            if (patterns == null || !patterns.Any())
                return true;

            return patterns.Any(p => p.IsMatch(title) || p.IsMatch(body));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SiteParser.Service.Services
{
    public static class PhoneExtractor
    {
        // Matches many phone formats, including some obfuscations
        private static readonly Regex PhoneRegex = new Regex(@"(?:\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}|\+?\d{10,15}", RegexOptions.Compiled);

        public static List<string> Extract(string text)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return results;

            var matches = PhoneRegex.Matches(text);
            foreach (Match match in matches)
            {
                results.Add(match.Value.Trim());
            }

            return results;
        }
    }
}

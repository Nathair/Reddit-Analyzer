using Microsoft.Playwright;
using SiteParser.Service.Models;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SiteParser.Service.Services
{
    public interface ISiteScraper
    {
        string SiteName { get; }
        Task<List<string>> CollectLinksAsync(string sourceUrl);
        Task<JobOffer> ScrapeDetailsAsync(IPage page, JobOffer offer);

        public string CleanPhone(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var cleaned = Regex.Replace(input, @"[^\d+]", "");

            cleaned = Regex.Replace(cleaned, @"(?!^)\+", "");

            return cleaned;
        }
    }
}

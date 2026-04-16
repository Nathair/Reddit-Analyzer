using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using SiteParser.Service.Models;

namespace SiteParser.Service.Services
{
    public interface ISiteScraper
    {
        string SiteName { get; }
        Task<List<string>> CollectLinksAsync(string sourceUrl);
        Task<JobOffer> ScrapeDetailsAsync(IPage page, JobOffer offer);
    }
}

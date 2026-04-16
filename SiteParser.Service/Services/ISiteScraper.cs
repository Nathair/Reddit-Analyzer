using System.Collections.Generic;
using System.Threading.Tasks;
using SiteParser.Service.Models;

namespace SiteParser.Service.Services
{
    public interface ISiteScraper
    {
        string SiteName { get; }
        Task<List<JobOffer>> ScrapeAsync();
    }
}

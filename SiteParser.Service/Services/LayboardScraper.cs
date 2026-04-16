using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using SiteParser.Service.Models;

namespace SiteParser.Service.Services
{
    public class LayboardScraper : ISiteScraper
    {
        public string SiteName => "Layboard";
        private readonly string _url = "https://layboard.com/ua/vakansii/ssha";

        public async Task<List<JobOffer>> ScrapeAsync()
        {
            var offers = new List<JobOffer>();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            Console.WriteLine($"[Layboard] Navigating to {_url}...");
            await page.GotoAsync(_url);

            // Get job links - selective for those that seem relevant
            var jobLinks = await page.QuerySelectorAllAsync(".job-item__title a");
            var urls = new List<string>();
            foreach (var link in jobLinks)
            {
                var href = await link.GetAttributeAsync("href");
                if (!string.IsNullOrEmpty(href))
                {
                    urls.Add(href.StartsWith("http") ? href : $"https://layboard.com{href}");
                }
            }

            Console.WriteLine($"[Layboard] Found {urls.Count} jobs. Scraping details...");

            foreach (var url in urls.Take(10)) 
            {
                try
                {
                    var offer = await ScrapeJobDetailsAsync(page, url);
                    if (offer != null)
                    {
                        offers.Add(offer);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Layboard] Error scraping {url}: {ex.Message}");
                }
            }

            return offers;
        }

        private async Task<JobOffer> ScrapeJobDetailsAsync(IPage page, string url)
        {
            await page.GotoAsync(url);
            
            var title = await page.InnerTextAsync("h1") ?? "No Title";
            var text = await page.InnerTextAsync(".vacancy-detail__description") ?? await page.InnerTextAsync("body");
            var location = await page.InnerTextAsync(".vacancy-detail__location") ?? "Not specified";

            var phones = PhoneExtractor.Extract(text);

            // Layboard often has European numbers as mentioned by user.
            // We extract everything and let the PhoneCheckService handle it.

            return new JobOffer
            {
                Title = title.Trim(),
                Text = text.Trim(),
                PhoneNumbers = phones.Distinct().ToList(),
                Location = location.Trim(),
                SourceUrl = url,
                ScrapedAt = DateTime.UtcNow
            };
        }
    }
}

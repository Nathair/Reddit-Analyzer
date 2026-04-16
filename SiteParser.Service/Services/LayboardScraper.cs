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

        public async Task<List<string>> CollectLinksAsync(string sourceUrl)
        {
            var urls = new List<string>();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            int i = 1;
            while (true)
            {
                var pageUrl = i == 1 ? sourceUrl : $"{sourceUrl}?page={i}";
                Console.WriteLine($"[{SiteName}] Collecting links from {pageUrl}...");

                try
                {
                    await page.GotoAsync(pageUrl);
                    var jobLinks = await page.QuerySelectorAllAsync(".job-item__title a");
                    if (jobLinks.Count == 0)
                    {
                        Console.WriteLine($"[{SiteName}] No more jobs found on page {i}. Stopping.");
                        break;
                    }

                    int countBefore = urls.Count;
                    foreach (var link in jobLinks)
                    {
                        var href = await link.GetAttributeAsync("href");
                        if (!string.IsNullOrEmpty(href))
                        {
                            urls.Add(href.StartsWith("http") ? href : $"https://layboard.com{href}");
                        }
                    }

                    if (urls.Count == countBefore) break;
                    i++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{SiteName}] Error on page {i}: {ex.Message}");
                    break;
                }
            }

            return urls.Distinct().ToList();
        }

        public async Task<JobOffer> ScrapeDetailsAsync(IPage page, JobOffer offer)
        {
            await page.GotoAsync(offer.JobUrl);

            var title = await page.InnerTextAsync("h1").ContinueWith(t => t.IsFaulted ? "No Title" : t.Result) ?? "No Title";
            var text = await page.InnerTextAsync(".vacancy-detail__description").ContinueWith(t => t.IsFaulted ? "" : t.Result)
                     ?? await page.InnerTextAsync("body").ContinueWith(t => t.IsFaulted ? "" : t.Result);

            var location = await page.InnerTextAsync(".vacancy-detail__location").ContinueWith(t => t.IsFaulted ? "Not specified" : t.Result)
                         ?? "Not specified";

            var phones = PhoneExtractor.Extract(text);

            return new JobOffer
            {
                Id = offer.Id,
                Title = title.Trim(),
                Text = text.Trim(),
                PhoneNumbers = phones.Distinct().ToList(),
                Location = location.Trim(),
                JobUrl = offer.JobUrl,
                ScrapedAt = DateTime.UtcNow,
                IsProcessed = true,
                Status = OfferStatus.Completed
            };
        }
    }
}

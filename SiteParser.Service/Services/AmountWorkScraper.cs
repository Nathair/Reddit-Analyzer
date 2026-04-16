using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using SiteParser.Service.Models;

namespace SiteParser.Service.Services
{
    public class AmountWorkScraper : ISiteScraper
    {
        public string SiteName => "Amountwork";
        private readonly string _url = "https://amountwork.com/ua/rabota/ssha/voditel";

        public async Task<List<JobOffer>> ScrapeAsync()
        {
            var offers = new List<JobOffer>();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            Console.WriteLine($"[Amountwork] Navigating to {_url}...");
            await page.GotoAsync(_url);

            // Get job links
            var jobLinks = await page.QuerySelectorAllAsync("h3 a");
            var urls = new List<string>();
            foreach (var link in jobLinks)
            {
                var href = await link.GetAttributeAsync("href");
                if (!string.IsNullOrEmpty(href))
                {
                    urls.Add(href.StartsWith("http") ? href : $"https://amountwork.com{href}");
                }
            }

            Console.WriteLine($"[Amountwork] Found {urls.Count} jobs. Scraping details...");

            foreach (var url in urls.Take(10)) // Limit for testing or first page
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
                    Console.WriteLine($"[Amountwork] Error scraping {url}: {ex.Message}");
                }
            }

            return offers;
        }

        private async Task<JobOffer> ScrapeJobDetailsAsync(IPage page, string url)
        {
            await page.GotoAsync(url);

            var title = await page.InnerTextAsync("h1") ?? "No Title";
            var text = await page.InnerTextAsync(".vacancy-description") ?? await page.InnerTextAsync("body");

            // Try to find location
            var location = await page.InnerTextAsync(".vacancy-container") ?? "Not specified";

            // Extract phones from text
            var phones = PhoneExtractor.Extract(text);

            // Check if there is a "Show phone" button
            var showPhoneBtn = await page.QuerySelectorAsync(".show-phone");
            if (showPhoneBtn != null)
            {
                await showPhoneBtn.ClickAsync();
                await Task.Delay(500); // Wait for reveal
                var revealedPhones = await page.InnerTextAsync(".phone-block");
                if (!string.IsNullOrEmpty(revealedPhones))
                {
                    phones.AddRange(PhoneExtractor.Extract(revealedPhones));
                }
            }

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

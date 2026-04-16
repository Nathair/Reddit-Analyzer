using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using SiteParser.Service.Models;
using PhoneNumbers;

namespace SiteParser.Service.Services
{
    public class AmountWorkScraper : ISiteScraper
    {
        public string SiteName => "Amountwork";

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
                    var jobLinks = await page.QuerySelectorAllAsync("h3 a");
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
                            urls.Add(href.StartsWith("http") ? href : $"https://amountwork.com{href}");
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
            var text = await page.InnerTextAsync(".vacancy-description").ContinueWith(t => t.IsFaulted ? "" : t.Result);

            var location = await page.InnerTextAsync(".company-info-country").ContinueWith(t => t.IsFaulted ? "Not specified" : t.Result.Split(":").Last())
                         ?? "Not specified";

            var phone = ("+" + await page.Locator(".company-info-contact").Last.InnerTextAsync().ContinueWith(t => t.IsFaulted ? "" : t.Result.Split(":").Last())).Replace("++", "+");

            var util = PhoneNumberUtil.GetInstance();
            string region = null;

            var phones = new List<string>();

            try
            {
                region = util.GetRegionCodeForNumber(util.Parse(phone, null));
                phones.Add(phone);
            }
            catch
            {

            }

            var matches = util.FindNumbers(text, region ?? "USA");

            foreach (var match in matches)
            {
                var number = match.Number;

                if (util.IsValidNumber(number))
                {
                    var formatted = util.Format(number, PhoneNumberFormat.E164);
                    phones.Add(formatted);
                }
            }

            offer.Title = title.Trim();
            offer.Text = text.Trim();
            offer.PhoneNumbers = phones.Distinct().ToList();
            offer.Location = location.Trim();
            offer.ScrapedAt = DateTime.UtcNow;
            offer.IsProcessed = true;
            offer.Status = OfferStatus.Completed;

            return offer;
        }
    }
}

using Microsoft.Playwright;
using PhoneNumbers;
using SiteParser.Service.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            await page.GotoAsync(sourceUrl);

            int i = 1;
            while (true)
            {
                Console.WriteLine($"[{SiteName}] Collecting links from page {i++}...");

                var btn = page.Locator(".js-more-btn.more-btn");
                var count = await btn.CountAsync();
                if (count == 0)
                {
                    break;
                }

                await btn.First.ClickAsync();
                await Task.Delay(1000);
            }

            try
            {
                var jobLinks = await page.QuerySelectorAllAsync(".card__body");
                if (jobLinks.Count == 0)
                {
                    Console.WriteLine($"[{SiteName}] No more jobs found on page {i}. Stopping.");
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{SiteName}] Error on page {i}: {ex.Message}");
            }

            return urls.Distinct().ToList();
        }

        public async Task<JobOffer> ScrapeDetailsAsync(IPage page, JobOffer offer)
        {
            await page.GotoAsync(offer.JobUrl);

            var title = await page.InnerTextAsync(".jarticle__title").ContinueWith(t => t.IsFaulted ? "No Title" : t.Result) ?? "No Title";
            var text = await page.InnerTextAsync(".jarticle__descrip").ContinueWith(t => t.IsFaulted ? "" : t.Result);

            var location = await page.Locator($"a.jarticle__stat-value[href]")
                     .InnerTextAsync();

            var phone = ((ISiteScraper)this).CleanPhone(("+" + await page.Locator(".js-phone-click").First.GetAttributeAsync("data-phone").ContinueWith(t => t.IsFaulted ? "" : t.Result.Split(":").Last())).Replace("++", "+"));

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

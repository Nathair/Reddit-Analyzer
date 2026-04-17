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
        private const int MaxPagesToLoad = 50;

        public async Task<List<string>> CollectLinksAsync(string sourceUrl)
        {
            var urls = new List<string>();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            try
            {
                await page.GotoAsync(sourceUrl, new PageGotoOptions { Timeout = 60000 });

                int pages = 0;
                while (pages < MaxPagesToLoad)
                {
                    var btn = page.Locator(".js-more-btn.more-btn");
                    if (await btn.CountAsync() == 0 || !await btn.IsVisibleAsync())
                    {
                        break;
                    }

                    Console.WriteLine($"[{SiteName}] Clicking 'More' (page {++pages})...");
                    await btn.First.ClickAsync();
                    await Task.Delay(1500);
                }

                var jobLinks = await page.QuerySelectorAllAsync(".card__body");
                if (jobLinks.Count == 0)
                {
                    Console.WriteLine($"[{SiteName}] No more jobs found on page {pages}. Stopping.");
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
                Console.WriteLine($"[{SiteName}] Error during link collection: {ex.Message}");
            }

            return urls.Distinct().ToList();
        }

        public async Task<JobOffer> ScrapeDetailsAsync(IPage page, JobOffer offer)
        {
            try
            {
                await page.GotoAsync(offer.JobUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{SiteName}] Error scraping details for {offer.JobUrl}: {ex.Message}");
                offer.Status = OfferStatus.Failed;
                offer.ErrorMessage = ex.Message;
            }

            return offer;
        }
    }
}

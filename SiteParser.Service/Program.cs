using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using SiteParser.Service.Models;
using SiteParser.Service.Services;

namespace SiteParser.Service
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Setup Logging
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/parser_.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("SiteParser Service Starting...");

            // Load Config
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var apiUrl = config["Settings:PhoneDbApiUrl"] ?? "http://localhost:5000/check_number";
            var outputFile = config["Settings:OutputFile"] ?? "leads.csv";
            var exclusionWords = config.GetSection("Settings:ExclusionWords").Get<string[]>() ?? Array.Empty<string>();

            // Services
            using var httpClient = new HttpClient();
            var phoneService = new PhoneCheckService(httpClient, apiUrl);
            var csvService = new CsvWriterService();
            
            var scrapers = new List<ISiteScraper>
            {
                new AmountWorkScraper(),
                new LayboardScraper()
            };

            while (true)
            {
                var allNewLeads = new List<JobOffer>();

                foreach (var scraper in scrapers)
                {
                    try
                    {
                        Log.Information("Starting scrape for {Site}", scraper.SiteName);
                        var offers = await scraper.ScrapeAsync();
                        Log.Information("Found {Count} offers from {Site}", offers.Count, scraper.SiteName);

                        foreach (var offer in offers)
                        {
                            // 1. Filter by exclusion words
                            if (exclusionWords.Any(w => (offer.Title?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false) || 
                                                        (offer.Text?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false)))
                            {
                                Log.Debug("Offer rejected by exclusion words: {Title}", offer.Title);
                                continue;
                            }

                            // 2. Filter by phone validation
                            var validPhones = new List<string>();
                            foreach (var phone in offer.PhoneNumbers)
                            {
                                if (await phoneService.IsValidAsync(phone))
                                {
                                    validPhones.Add(phone);
                                }
                            }

                            if (validPhones.Any())
                            {
                                offer.PhoneNumbers = validPhones;
                                allNewLeads.Add(offer);
                                Log.Information("New valid lead found: {Title} with {Phones}", offer.Title, string.Join(", ", validPhones));
                            }
                            else
                            {
                                Log.Debug("Offer rejected: no valid or new phone numbers found for {Title}", offer.Title);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error during scraping {Site}", scraper.SiteName);
                    }
                }

                if (allNewLeads.Any())
                {
                    csvService.WriteLeads(outputFile, allNewLeads);
                    Log.Information("Saved {Count} new leads to {File}", allNewLeads.Count, outputFile);
                }
                else
                {
                    Log.Information("No new leads found in this run.");
                }

                var interval = int.Parse(config["Settings:ScrapingIntervalMinutes"] ?? "60");
                Log.Information("Sleeping for {Interval} minutes...", interval);
                await Task.Delay(TimeSpan.FromMinutes(interval));
            }
        }
    }
}

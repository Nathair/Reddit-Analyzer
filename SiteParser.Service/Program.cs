using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
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

            Log.Information("SiteParser Service Starting (Full Cycle Mode)...");

            // Load Config
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var apiUrl = config["Settings:PhoneDbApiUrl"] ?? "http://localhost:5000/check_number";
            var outputFile = config["Settings:OutputFile"] ?? "leads.csv";
            var exclusionWords = config.GetSection("Settings:ExclusionWords").Get<string[]>() ?? Array.Empty<string>();
            var sourcesFile = "sources.txt";

            // Mongo Setup
            var mongoConn = config["Settings:MongoDb:ConnectionString"] ?? "mongodb://localhost:27017";
            var mongoDbName = config["Settings:MongoDb:DatabaseName"] ?? "JobParserDb";
            var mongoColl = config["Settings:MongoDb:CollectionName"] ?? "ProcessedOffers";
            var mongoService = new MongoService(mongoConn, mongoDbName, mongoColl);

            // Services
            using var httpClient = new HttpClient();
            var phoneService = new PhoneCheckService(httpClient, apiUrl);
            var csvService = new CsvWriterService();

            var amountworkScraper = new AmountWorkScraper();
            var layboardScraper = new LayboardScraper();

            while (true)
            {
                await CollectJobOffers(sourcesFile, mongoService, amountworkScraper, layboardScraper);
                await ProcessingDiscoveredOffers(mongoService, amountworkScraper, layboardScraper);
                await GenerateCsv(outputFile, exclusionWords, mongoService, phoneService, csvService);

                var interval = int.Parse(config["Settings:ScrapingIntervalMinutes"] ?? "60");
                Log.Information("Cycle complete. Waiting {Interval} minutes...", interval);
                await Task.Delay(TimeSpan.FromMinutes(interval));
            }
        }

        private static async Task GenerateCsv(string outputFile, string[] exclusionWords, MongoService mongoService, PhoneCheckService phoneService, CsvWriterService csvService)
        {
            Log.Information("=== STAGE 3: Processing for csv ===");
            var pendingOffers = await mongoService.GetProcessedOffersAsync(1000);

            if (pendingOffers.Any())
            {
                var newLeadsForCsv = new List<JobOffer>();

                foreach (var detailedOffer in pendingOffers)
                {
                    try
                    {
                        if (exclusionWords.Any(w => (detailedOffer.Title?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                                    (detailedOffer.Text?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false)))
                        {
                            detailedOffer.Status = OfferStatus.Excluded;
                            await mongoService.SaveOfferAsync(detailedOffer);
                            continue;
                        }

                        var validPhones = new List<string>();
                        foreach (var phone in detailedOffer.PhoneNumbers)
                        {
                            if (await phoneService.IsValidAsync(phone))
                            {
                                validPhones.Add(phone);
                            }
                        }

                        if (validPhones.Any())
                        {
                            detailedOffer.PhoneNumbers = validPhones;
                            detailedOffer.Status = OfferStatus.Completed;
                            detailedOffer.ProcessedAt = DateTime.UtcNow;

                            newLeadsForCsv.Add(detailedOffer);
                            await mongoService.SaveOfferAsync(detailedOffer);
                            Log.Information("Lead SAVED: {Title}", detailedOffer.Title);
                        }
                        else
                        {
                            detailedOffer.Status = OfferStatus.Rejected;
                            await mongoService.SaveOfferAsync(detailedOffer);
                            Log.Debug("Lead REJECTED (no valid phones): {Title}", detailedOffer.Title);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error processing {Url}", detailedOffer.JobUrl);
                        detailedOffer.Status = OfferStatus.Failed;
                        detailedOffer.ErrorMessage = ex.Message;
                        await mongoService.SaveOfferAsync(detailedOffer);
                    }
                }

                if (newLeadsForCsv.Any())
                {
                    csvService.WriteLeads(outputFile, newLeadsForCsv);
                    Log.Information("Appended {Count} leads to {File}", newLeadsForCsv.Count, outputFile);
                }
            }
        }

        private static async Task<List<JobOffer>> ProcessingDiscoveredOffers(MongoService mongoService, AmountWorkScraper amountworkScraper, LayboardScraper layboardScraper)
        {
            Log.Information("=== STAGE 2: Processing discovered offers ===");
            var pendingOffers = await mongoService.GetUnprocessedOffersAsync(1000);
            Log.Information("Found {Count} pending offers in DB", pendingOffers.Count);

            if (pendingOffers.Any())
            {
                using var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                var page = await browser.NewPageAsync();

                foreach (var offer in pendingOffers)
                {
                    try
                    {
                        ISiteScraper? scraper = null;
                        if (offer.JobUrl!.Contains("amountwork.com")) scraper = amountworkScraper;
                        else if (offer.JobUrl!.Contains("layboard.com")) scraper = layboardScraper;

                        if (scraper == null) continue;

                        Log.Information("Processing: {Url}", offer.JobUrl);
                        var detailedOffer = await scraper.ScrapeDetailsAsync(page, offer);
                        await mongoService.SaveOfferAsync(detailedOffer);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error processing {Url}", offer.JobUrl);
                        offer.Status = OfferStatus.Failed;
                        offer.ErrorMessage = ex.Message;
                        offer.IsProcessed = true;
                        await mongoService.SaveOfferAsync(offer);
                    }
                }
            }

            return pendingOffers;
        }

        private static async Task CollectJobOffers(string sourcesFile, MongoService mongoService, AmountWorkScraper amountworkScraper, LayboardScraper layboardScraper)
        {
            Log.Information("=== STAGE 1: Collecting all links from sources ===");
            var sources = File.Exists(sourcesFile) ? File.ReadAllLines(sourcesFile) : Array.Empty<string>();

            foreach (var sourceUrl in sources.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                ISiteScraper? scraper = null;
                if (sourceUrl.Contains("amountwork.com")) scraper = amountworkScraper;
                else if (sourceUrl.Contains("layboard.com")) scraper = layboardScraper;

                if (scraper == null) continue;

                try
                {
                    var links = await scraper.CollectLinksAsync(sourceUrl);
                    Log.Information("Collected {Count} links from {Url}", links.Count, sourceUrl);

                    foreach (var link in links)
                    {
                        if (!await mongoService.IsProcessedAsync(link))
                        {
                            await mongoService.SaveOfferAsync(new JobOffer()
                            {
                                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                                SourceUrl = sourceUrl,
                                JobUrl = link,
                                ScrapedAt = DateTime.UtcNow,
                                Status = OfferStatus.Discovered,
                                IsProcessed = false
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error during collection from {Url}", sourceUrl);
                }
            }
        }
    }
}

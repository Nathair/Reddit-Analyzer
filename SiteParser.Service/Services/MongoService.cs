using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using SiteParser.Service.Models;

namespace SiteParser.Service.Services
{
    public class MongoService
    {
        private readonly IMongoCollection<JobOffer> _collection;

        public MongoService(string connectionString, string databaseName, string collectionName)
        {
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _collection = database.GetCollection<JobOffer>(collectionName);

            // Create index on SourceUrl for faster checks
            var indexKeysDefinition = Builders<JobOffer>.IndexKeys.Ascending(offer => offer.JobUrl);
            var indexOptions = new CreateIndexOptions { Unique = true };
            var indexModel = new CreateIndexModel<JobOffer>(indexKeysDefinition, indexOptions);
            _collection.Indexes.CreateOne(indexModel);
        }

        public async Task<bool> IsProcessedAsync(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            var filter = Builders<JobOffer>.Filter.Eq(offer => offer.JobUrl, url);
            return await _collection.Find(filter).AnyAsync();
        }

        public async Task SaveOfferAsync(JobOffer offer)
        {
            try
            {
                var res = await _collection.ReplaceOneAsync(
                    o => o.JobUrl == offer.JobUrl,
                    offer,
                    new ReplaceOptions { IsUpsert = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Mongo] Error saving {offer.JobUrl}: {ex.Message}");
            }
        }

        public async Task<List<JobOffer>> GetUnprocessedOffersAsync(int limit = 100)
        {
            var filter = Builders<JobOffer>.Filter.Eq(offer => offer.IsProcessed, false);
            return await _collection.Find(filter).Limit(limit).ToListAsync();
        }

        public async Task<List<JobOffer>> GetProcessedOffersAsync(int limit = 100)
        {
            return await _collection.Find(offer => offer.IsProcessed && offer.Status != OfferStatus.Excluded).Limit(limit).ToListAsync();
        }
    }
}

using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace SiteParser.Service.Models
{
    public enum OfferStatus
    {
        Discovered,
        Completed,
        Failed,
        Excluded,
        Rejected
    }

    [BsonIgnoreExtraElements]
    public class JobOffer
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public string? Title { get; set; }
        public string? Text { get; set; }
        public List<string> PhoneNumbers { get; set; } = new List<string>();
        public string? Email { get; set; }
        public string? Location { get; set; }
        public string? SourceUrl { get; set; }
        public string? JobUrl { get; set; }
        public DateTime ScrapedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        [BsonRepresentation(BsonType.String)]
        public OfferStatus Status { get; set; } = OfferStatus.Discovered;
        public string? ErrorMessage { get; set; }
        public bool IsProcessed { get; set; } = false;

        public override string ToString()
        {
            return $"{Title} ({Location}) - {string.Join(", ", PhoneNumbers)}";
        }
    }
}

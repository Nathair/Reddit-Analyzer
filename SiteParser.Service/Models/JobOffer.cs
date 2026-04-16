using System;
using System.Collections.Generic;

namespace SiteParser.Service.Models
{
    public class JobOffer
    {
        public string? Title { get; set; }
        public string? Text { get; set; }
        public List<string> PhoneNumbers { get; set; } = new List<string>();
        public string? Email { get; set; }
        public string? Location { get; set; }
        public string? SourceUrl { get; set; }
        public DateTime ScrapedAt { get; set; }

        public override string ToString()
        {
            return $"{Title} ({Location}) - {string.Join(", ", PhoneNumbers)}";
        }
    }
}

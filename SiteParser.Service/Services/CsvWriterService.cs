using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using SiteParser.Service.Models;

namespace SiteParser.Service.Services
{
    public class CsvWriterService
    {
        public void WriteLeads(string filePath, List<JobOffer> leads)
        {
            var exists = File.Exists(filePath);
            using var writer = new StreamWriter(filePath, append: true);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            if (!exists)
            {
                csv.WriteHeader<JobOffer>();
                csv.NextRecord();
            }

            foreach (var lead in leads)
            {
                csv.WriteRecord(lead);
                csv.NextRecord();
            }
        }
    }
}

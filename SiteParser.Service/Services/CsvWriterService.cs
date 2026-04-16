using CsvHelper;
using CsvHelper.Configuration;
using SiteParser.Service.Models;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SiteParser.Service.Services
{
    public class CsvWriterService
    {
        public void WriteLeads(string filePath, List<JobOffer> leads)
        {
            var exists = File.Exists(filePath);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";" // ← тут задаєш
            };

            using var writer = new StreamWriter(filePath, append: true);
            using var csv = new CsvWriter(writer, config);

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

using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace FileMonitoring.Api.Services
{
    public interface IFileMonitorService
    {
        void Initialize();
        int CountRecords();
        (byte[] Content, string Filename)? ExportAndArchive();
    }

    public class FileMonitorService : IFileMonitorService
    {
        private readonly string _filePath;
        private readonly CsvConfiguration _csvConfig;
        private readonly ILogger<FileMonitorService> _logger;
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        public FileMonitorService(IConfiguration config, ILogger<FileMonitorService> logger)
        {
            _filePath = config["FileMonitoring:FilePath"] ?? "data.csv";
            _logger = logger;
            _csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null
            };
        }

        public void Initialize()
        {
            _logger.LogInformation("Initializing file monitoring environment...");
            Directory.CreateDirectory("out");

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                _logger.LogInformation("Creating directory: {Directory}", directory);
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(_filePath))
            {
                _logger.LogInformation("Creating initial empty file: {FilePath}", _filePath);
                File.WriteAllText(_filePath, string.Empty);
            }
        }

        public int CountRecords()
        {
            _logger.LogDebug("Starting record count for file: {FilePath}", _filePath);
            _fileLock.Wait();
            try
            {
                if (!File.Exists(_filePath) || new FileInfo(_filePath).Length == 0)
                {
                    _logger.LogInformation("File {FilePath} not found or empty. Count: 0", _filePath);
                    return 0;
                }

                using var reader = new StreamReader(_filePath);
                using var csv = new CsvReader(reader, _csvConfig);

                if (!csv.Read() || !csv.ReadHeader())
                {
                    _logger.LogWarning("File {FilePath} has no header or data.", _filePath);
                    return 0;
                }

                int count = 0;
                while (csv.Read())
                {
                    count++;
                }
                _logger.LogInformation("Counted {Count} records in {FilePath}", count, _filePath);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting records in {FilePath}", _filePath);
                return 0;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public (byte[] Content, string Filename)? ExportAndArchive()
        {
            _logger.LogInformation("Export and archive requested for: {FilePath}", _filePath);
            _fileLock.Wait();
            try
            {
                if (!File.Exists(_filePath))
                {
                    _logger.LogWarning("Export failed: File {FilePath} not found.", _filePath);
                    return null;
                }

                var timestamp = DateTime.Now.ToString("yyyy.MM.dd_HH.mm");
                var originalFileName = Path.GetFileName(_filePath);
                var archivedFileName = $"{timestamp}_{originalFileName}";
                var archivePath = Path.Combine("out", archivedFileName);

                byte[] content = File.ReadAllBytes(_filePath);
                File.Move(_filePath, archivePath, overwrite: true);

                _logger.LogInformation("File {Original} successfully moved to {Archive}", _filePath, archivePath);

                Initialize();

                return (content, archivedFileName);
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
}

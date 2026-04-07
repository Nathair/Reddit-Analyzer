using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace RedditAnalyzer.Server.Services
{
    public interface IChromiumBrowserManager : IAsyncDisposable
    {
        Task<IBrowser> GetBrowserAsync();
    }

    public class ChromiumBrowserManager : IChromiumBrowserManager
    {
        private IBrowser? _browser;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ILogger<ChromiumBrowserManager> _logger;

        public ChromiumBrowserManager(ILogger<ChromiumBrowserManager> logger)
        {
            _logger = logger;
        }

        public async Task<IBrowser> GetBrowserAsync()
        {
            if (_browser?.IsConnected == true) return _browser;

            await _lock.WaitAsync();
            try
            {
                if (_browser?.IsConnected == true) return _browser;

                var fetcher = new BrowserFetcher();
                if (!System.Linq.Enumerable.Any(fetcher.GetInstalledBrowsers()))
                {
                    _logger.LogInformation("No Chromium found. Downloading latest version...");
                    await fetcher.DownloadAsync();
                }

                _logger.LogInformation("Launching a shared browser instance...");
                _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
                });

                return _browser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to launch Chromium browser.");
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (_browser != null)
                {
                    _logger.LogInformation("Shutting down the shared browser instance...");
                    await _browser.DisposeAsync();
                    _browser = null;
                }
            }
            finally
            {
                _lock.Release();
                _lock.Dispose();
            }
        }
    }

    public static class ChromiumExtensions
    {
        public static async Task WarmupChromiumAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var browserManager = scope.ServiceProvider.GetRequiredService<IChromiumBrowserManager>();
            await browserManager.GetBrowserAsync();
        }
    }
}

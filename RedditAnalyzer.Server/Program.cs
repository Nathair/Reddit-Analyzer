using Serilog;
using RedditAnalyzer.Server.Services;
using RedditAnalyzer.Server.Middleware;
using Microsoft.Extensions.Http;

namespace RedditAnalyzer.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine("logs", "out.log"), rollingInterval: RollingInterval.Day)
                .CreateLogger();

            builder.Host.UseSerilog();

            builder.Services.AddControllers();
            builder.Services.AddHttpClient("RedditClient", client =>
            {
                client.BaseAddress = new Uri("https://www.reddit.com/");
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            });

            builder.Services.AddSingleton<IChromiumBrowserManager, ChromiumBrowserManager>();
            builder.Services.AddScoped<IRedditClient, RedditClient>();
            builder.Services.AddScoped<IPostMatcher, PostMatcher>();
            builder.Services.AddScoped<IRedditService, RedditService>();

            builder.Services.AddAutoMapper(typeof(Program));

            var app = builder.Build();

            await app.WarmupChromiumAsync();

            app.UseMiddleware<ExceptionMiddleware>();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseAuthorization();

            app.MapControllers();
            app.MapFallbackToFile("/index.html");

            try
            {
                Log.Information("Starting web host");
                await app.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}

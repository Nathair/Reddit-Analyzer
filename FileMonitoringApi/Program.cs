using FileMonitoring.Api.Services;

namespace FileMonitoring.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddScoped<IFileMonitorService, FileMonitorService>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IFileMonitorService>();
                service.Initialize();
            }

            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}

using Microsoft.EntityFrameworkCore;
using PhoneDb.Api.Data;
using PhoneDb.Api.Services;

namespace PhoneDb.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddMemoryCache();

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddScoped<IPhoneService, PhoneService>();
            builder.Services.AddSingleton<ILockService, LockService>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                context.Database.EnsureCreated();
            }

            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}

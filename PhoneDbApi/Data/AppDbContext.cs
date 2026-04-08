using Microsoft.EntityFrameworkCore;
using PhoneDb.Api.Models;

namespace PhoneDb.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<PhoneNumberEntry> PhoneNumbers => Set<PhoneNumberEntry>();
    }
}

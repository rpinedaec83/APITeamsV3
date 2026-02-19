using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace APITeamsV3.Infrastructure.Persistence.Contexts
{
    public class CentralDbContextFactory : IDesignTimeDbContextFactory<CentralDbContext>
    {
        public CentralDbContext CreateDbContext(string[] args)
        {
            // Build config
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.Development.json", optional: true)
                .Build();

            var builder = new DbContextOptionsBuilder<CentralDbContext>();
            var connectionString = configuration.GetConnectionString("CentralConnection");
            
            // Fallback for design time if appsettings not found or empty
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = "Data Source=APITeamsV3_Central.db";
            }

            builder.UseSqlite(connectionString);

            return new CentralDbContext(builder.Options);
        }
    }
}

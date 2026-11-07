using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Cortex.Infrastructure.Persistence
{
    public class CortexDbDesignTimeFactory : IDesignTimeDbContextFactory<CortexDbContext>
    {
        public CortexDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<CortexDbDesignTimeFactory>()
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' not found. " +
                    "Ensure it is set in user secrets or environment variables.");

            var optionsBuilder = new DbContextOptionsBuilder<CortexDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new CortexDbContext(optionsBuilder.Options);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace MathLearning.Infrastructure.Persistance;

public class ApiDbContextFactory : IDesignTimeDbContextFactory<ApiDbContext>
{
    public ApiDbContext CreateDbContext(string[] args)
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var directory = Path.GetDirectoryName(assemblyLocation)!;

        // Pokušaj da nađe appsettings.json u različitim lokacijama
        var configuration = new ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile(Path.Combine(directory, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(directory, "..", "MathLearning.Api", "appsettings.json"), optional: true)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            // Fallback za design-time - koristi default connection string
            connectionString = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=mathlearning";
        }

        var builder = new DbContextOptionsBuilder<ApiDbContext>();
        builder.UseNpgsql(connectionString);

        return new ApiDbContext(builder.Options);
    }
}

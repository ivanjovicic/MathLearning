using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace MathLearning.Infrastructure.Persistance;

public class ApiDbContextFactory : IDesignTimeDbContextFactory<ApiDbContext>
{
    public ApiDbContext CreateDbContext(string[] args)
    {
        var envConn = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                      ?? Environment.GetEnvironmentVariable("EFDATABASE")
                      ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        string connectionString;

        if (!string.IsNullOrWhiteSpace(envConn))
        {
            connectionString = envConn;
        }
        else
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

            connectionString = configuration.GetConnectionString("Default")
                ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=mathlearning_dev;";
        }

        var builder = new DbContextOptionsBuilder<ApiDbContext>();
        builder.UseNpgsql(connectionString);

        return new ApiDbContext(builder.Options);
    }
}

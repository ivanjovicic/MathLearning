using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace MathLearning.Infrastructure.Persistance;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Pokušaj 1: Traži appsettings.json relativno od Infrastructure projekta
        var currentDir = Directory.GetCurrentDirectory();
        var apiProjectPath = Path.GetFullPath(Path.Combine(currentDir, "..", "MathLearning.Api"));
        
        // Pokušaj 2: Ako je current dir root solution folder
        if (!Directory.Exists(apiProjectPath))
        {
            apiProjectPath = Path.GetFullPath(Path.Combine(currentDir, "src", "MathLearning.Api"));
        }
        
        // Pokušaj 3: Ako je current dir već u src folderu
        if (!Directory.Exists(apiProjectPath))
        {
            apiProjectPath = Path.GetFullPath(Path.Combine(currentDir, "MathLearning.Api"));
        }

        if (!Directory.Exists(apiProjectPath))
        {
            throw new InvalidOperationException($"Cannot find MathLearning.Api project at: {apiProjectPath}");
        }

        var configPath = Path.Combine(apiProjectPath, "appsettings.json");
        if (!File.Exists(configPath))
        {
            throw new InvalidOperationException($"Cannot find appsettings.json at: {configPath}");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' not found in appsettings.json");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}

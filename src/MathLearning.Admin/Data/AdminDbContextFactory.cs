using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Reflection;

namespace MathLearning.Admin.Data;

public class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        // ✔ Uzima folder gde se nalazi MathLearning.Admin.dll
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var directory = Path.GetDirectoryName(assemblyLocation)!;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("AdminIdentity")
            ?? throw new Exception("Connection string 'AdminIdentity' not found.");

        var builder = new DbContextOptionsBuilder<AdminDbContext>();
        builder.UseNpgsql(connectionString);

        return new AdminDbContext(builder.Options);
    }
}

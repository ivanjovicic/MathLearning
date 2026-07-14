using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MathLearning.Tests.Helpers;

public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly string maintenanceConnectionString;

    private PostgresTestDatabase(string maintenanceConnectionString, string databaseName)
    {
        this.maintenanceConnectionString = maintenanceConnectionString;
        DatabaseName = databaseName;

        var builder = new NpgsqlConnectionStringBuilder(maintenanceConnectionString)
        {
            Database = databaseName,
            Pooling = true
        };
        DatabaseConnectionString = builder.ConnectionString;
    }

    public string DatabaseName { get; }

    public string DatabaseConnectionString { get; }

    public static async Task<PostgresTestDatabase> CreateAsync()
    {
        var database = new PostgresTestDatabase(
            ResolveMaintenanceConnectionString(),
            $"mathlearning_pgtest_{Guid.NewGuid():N}");

        await database.CreateDatabaseAsync();
        return database;
    }

    public DbContextOptions<ApiDbContext> CreateApiOptions()
    {
        return new DbContextOptionsBuilder<ApiDbContext>()
            .UseNpgsql(
                DatabaseConnectionString,
                npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .Options;
    }

    public DbContextOptions<AppDbContext> CreateAppOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                DatabaseConnectionString,
                npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .Options;
    }

    public async Task MigrateApiAsync()
    {
        await using var db = new ApiDbContext(CreateApiOptions());
        await db.Database.MigrateAsync();
    }

    public async Task SeedApiAsync()
    {
        await using var db = new ApiDbContext(CreateApiOptions());
        await TestDbContextFactory.SeedAsync(db);
    }

    public async Task<ApiDbContext> CreateSeededApiDbContextAsync()
    {
        var db = new ApiDbContext(CreateApiOptions());
        await db.Database.MigrateAsync();
        await TestDbContextFactory.SeedAsync(db);
        return db;
    }

    public async ValueTask DisposeAsync()
    {
        await DropDatabaseAsync();
    }

    private async Task CreateDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(maintenanceConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $@"CREATE DATABASE ""{DatabaseName}"";";
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(maintenanceConnectionString);
        await connection.OpenAsync();

        await using var terminate = connection.CreateCommand();
        terminate.CommandText =
            """
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = @databaseName
              AND pid <> pg_backend_pid();
            """;
        terminate.Parameters.AddWithValue("databaseName", DatabaseName);
        await terminate.ExecuteNonQueryAsync();

        await using var drop = connection.CreateCommand();
        drop.CommandText = $@"DROP DATABASE IF EXISTS ""{DatabaseName}"";";
        await drop.ExecuteNonQueryAsync();
    }

    private static string ResolveMaintenanceConnectionString()
    {
        return Environment.GetEnvironmentVariable("TEST_POSTGRES_MAINTENANCE_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres;Pooling=true;Timeout=15;Command Timeout=15";
    }
}

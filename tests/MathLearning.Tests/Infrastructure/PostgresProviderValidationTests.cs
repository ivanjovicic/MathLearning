using MathLearning.Api.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Database;

public sealed class PostgresProviderValidationTests
{
    [Fact]
    [Trait("Category", "PostgresProvider")]
    public async Task RefreshTokenGeneratedValue_PersistsOnPostgres()
    {
        if (!IsValidationRequired())
        {
            return;
        }

        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();
        await database.SeedApiAsync();

        await using var db = new ApiDbContext(database.CreateApiOptions());
        var refreshToken = RefreshTokenService.CreateRefreshToken("1", "postgres-provider-stamp", device: "postgres-provider", ipAddress: "127.0.0.1");

        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        var persisted = await db.RefreshTokens.SingleAsync(x => x.Id == refreshToken.Id);

        Assert.Equal(88, refreshToken.Token.Length);
        Assert.Equal(refreshToken.Token, persisted.Token);
        Assert.Equal("postgres-provider", persisted.Device);
        Assert.Equal("127.0.0.1", persisted.IpAddress);
    }

    [Fact]
    [Trait("Category", "PostgresProvider")]
    public async Task FreshMigratedPostgresDatabase_PassesSchemaGuard()
    {
        if (!IsValidationRequired())
        {
            return;
        }

        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();

        await using var db = new ApiDbContext(database.CreateApiOptions());
        var guard = new DatabaseSchemaVersionGuard(NullLogger<DatabaseSchemaVersionGuard>.Instance);

        var status = await guard.CheckAsync(db);

        Assert.True(status.IsSchemaReady, status.FailureMessage ?? "Expected migrated PostgreSQL database to match the compiled model.");
        Assert.Empty(status.PendingMigrations);
        Assert.Empty(status.UnknownAppliedMigrations);
    }

    private static bool IsValidationRequired()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("POSTGRES_PROVIDER_TESTS_REQUIRED"),
            "1",
            StringComparison.Ordinal);
    }
}

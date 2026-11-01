using System.Reflection;
using MathLearning.Api.Services;
using MathLearning.Infrastructure.Migrations.Api;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MathLearning.Tests.Database;

/// <summary>Issue #47 regression guards: migration registration, model/critical-object parity, physical parity.</summary>
public sealed class MigrationDriftRegressionTests
{
    // Known legacy Migration classes without [Migration]/Designer. They were never applied by EF.
    // Do NOT add attributes retroactively without separate analysis (see docs/migration-drift-issue-47.md).
    // Their critical objects are reconciled by 20261101185317_RepairUserAnswerAuditXpSchema or tracked separately.
    private static readonly string[] KnownLegacyUnregisteredMigrations =
    {
        "AddUserAnswerAudit",
        "AddUserQuestionAttemptRateLimiting",
        "AddPracticeSessionPipeline",
        "HardenXpAuditPipeline",
        "AddSyncDeadLetterRedriveLifecycle",
        "AddPerformanceOptimizations",
        "AddEconomyTransactionOperationId",
        // Root Migrations folder (AppDbContext), no Designer; outside the Api drift scope of issue #47. Tracked separately.
        "AddOptionOrder",
    };

    private static ApiDbContext CreateOfflineContext() =>
        new(new DbContextOptionsBuilder<ApiDbContext>()
            .UseNpgsql("Host=localhost;Database=offline;Username=none;Password=none")
            .Options);

    [Fact]
    public void NoNewUnregisteredMigrationClasses()
    {
        var unregistered = typeof(ApiDbContext).Assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<MigrationAttribute>() is null)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(KnownLegacyUnregisteredMigrations.OrderBy(n => n, StringComparer.Ordinal), unregistered);
    }

    [Fact]
    public void EveryRegisteredApiMigrationTargetsApiDbContext()
    {
        var missingContext = typeof(ApiDbContext).Assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && t.GetCustomAttribute<MigrationAttribute>() is not null)
            .Where(t => t.Namespace == typeof(RepairUserAnswerAuditXpSchema).Namespace)
            .Where(t => t.GetCustomAttribute<DbContextAttribute>()?.ContextType != typeof(ApiDbContext))
            .Select(t => t.Name)
            .ToArray();

        Assert.Empty(missingContext);
    }

    [Fact]
    public void RepairMigrationIsRegisteredAndLast()
    {
        using var db = CreateOfflineContext();
        var migrations = db.Database.GetMigrations().ToArray();

        Assert.Equal("20261101185317_RepairUserAnswerAuditXpSchema", migrations[^1]);
    }

    [Fact]
    public void RepairMigrationSqlIsGuardedAndAdditive()
    {
        var sql = RepairUserAnswerAuditXpSchema.UpSql;

        Assert.Contains("lock_timeout", sql);
        Assert.Contains("statement_timeout", sql);
        Assert.Contains("duplicate first-correct", sql);
        Assert.Contains("CREATE UNIQUE INDEX IF NOT EXISTS \"UX_UserAnswerAudits_FirstCorrect_PerQuestion\"", sql);
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CONCURRENTLY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("__EFMigrationsHistory", sql, StringComparison.Ordinal);
        foreach (var column in new[] { "ClientId", "CreatedAt", "IsFirstTimeCorrect", "IsOffline", "Reason", "Source", "TotalXpAfterAward" })
        {
            Assert.Contains($"ADD COLUMN IF NOT EXISTS \"{column}\"", sql);
        }
    }

    [Fact]
    public void CriticalPhysicalObjectsExistInCompiledModel()
    {
        using var db = CreateOfflineContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var modelObjects = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entity in model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null) continue;
            var storeObject = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var property in entity.GetProperties())
            {
                modelObjects.Add($"column:{table}.{property.GetColumnName(storeObject)}");
            }
            foreach (var index in entity.GetIndexes())
            {
                modelObjects.Add($"index:{index.GetDatabaseName()}");
            }
        }

        var notInModel = DatabaseSchemaVersionGuard.CriticalPhysicalObjects.Where(o => !modelObjects.Contains(o)).ToArray();
        Assert.Empty(notInModel);
    }

    [Fact]
    [Trait("Category", "DatabaseSchema")]
    public async Task PhysicalSchemaHasCriticalObjectsAndModelColumns()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SCHEMA_VALIDATION_REQUIRED"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var connectionString = Environment.GetEnvironmentVariable("DATABASE_SCHEMA_VALIDATION_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException("Schema validation connection string is not configured.");
        await using var db = new ApiDbContext(new DbContextOptionsBuilder<ApiDbContext>().UseNpgsql(connectionString).Options);

        var missingCritical = await DatabaseSchemaVersionGuard.FindMissingCriticalObjectsAsync(db, CancellationToken.None);
        Assert.Empty(missingCritical);

        // Model-vs-physical: every mapped column of quiz settlement tables must physically exist.
        var physical = (await db.Database.SqlQueryRaw<string>(
                "SELECT table_name || '.' || column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = current_schema()")
            .ToListAsync()).ToHashSet(StringComparer.Ordinal);
        var model = db.GetService<IDesignTimeModel>().Model;
        var missingModelColumns = new List<string>();
        foreach (var table in new[] { "UserAnswerAudits", "economy_transactions" })
        {
            var entity = model.GetEntityTypes().Single(e => e.GetTableName() == table);
            var so = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table, entity.GetSchema());
            missingModelColumns.AddRange(entity.GetProperties()
                .Select(p => $"{table}.{p.GetColumnName(so)}")
                .Where(c => !physical.Contains(c)));
        }

        Assert.Empty(missingModelColumns);
    }
}

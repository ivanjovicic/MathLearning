using System.Reflection;
using MathLearning.Api.Services;
using MathLearning.Infrastructure.Migrations.Api;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
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

    private static DatabaseSchemaVersionGuard.CriticalIndexCatalogRow HealthyRow(DatabaseSchemaVersionGuard.CriticalIndexSpec spec) => new()
    {
        IndexName = spec.Name, TableName = spec.Table, IsUnique = spec.Unique, IsValid = true, IsReady = true,
        HasExpressions = false, Columns = spec.Columns, Predicate = spec.Predicate,
    };

    private const string FirstCorrectUx = "UX_UserAnswerAudits_FirstCorrect_PerQuestion";

    private static string[] EvaluateWithMutatedUx(Action<DatabaseSchemaVersionGuard.CriticalIndexCatalogRow> mutate)
    {
        var rows = DatabaseSchemaVersionGuard.CriticalIndexSpecs.Select(HealthyRow).ToList();
        mutate(rows.Single(r => r.IndexName == FirstCorrectUx));
        return DatabaseSchemaVersionGuard.EvaluateCriticalIndexes(rows).ToArray();
    }

    [Fact]
    public void CriticalIndexEvaluation_AcceptsExactDefinitions()
    {
        var rows = DatabaseSchemaVersionGuard.CriticalIndexSpecs.Select(HealthyRow);
        Assert.Empty(DatabaseSchemaVersionGuard.EvaluateCriticalIndexes(rows));
    }

    [Fact]
    public void CriticalIndexEvaluation_ReportsMissingIndex()
    {
        var rows = DatabaseSchemaVersionGuard.CriticalIndexSpecs.Select(HealthyRow).Where(r => r.IndexName != FirstCorrectUx);
        Assert.Equal(new[] { $"index:{FirstCorrectUx}" }, DatabaseSchemaVersionGuard.EvaluateCriticalIndexes(rows));
    }

    [Theory]
    [InlineData("invalid", "indisvalid=false")]
    [InlineData("notready", "indisready=false")]
    [InlineData("nonunique", "unique=false")]
    [InlineData("nopredicate", "predicate=<none>")]
    [InlineData("wrongpredicate", "predicate=(\"IsFirstTimeCorrect\" = false)")]
    [InlineData("wrongcolumns", "columns=UserId,QuestionId")]
    [InlineData("expression", "expression keys")]
    [InlineData("wrongtable", "table=UserAnswers")]
    public void CriticalIndexEvaluation_RejectsSameNameButWrongIndex(string scenario, string expectedDefect)
    {
        var problems = EvaluateWithMutatedUx(row =>
        {
            switch (scenario)
            {
                case "invalid": row.IsValid = false; break;
                case "notready": row.IsReady = false; break;
                case "nonunique": row.IsUnique = false; break;
                case "nopredicate": row.Predicate = null; break;
                case "wrongpredicate": row.Predicate = "(\"IsFirstTimeCorrect\" = false)"; break;
                case "wrongcolumns": row.Columns = "UserId,QuestionId"; break;
                case "expression": row.HasExpressions = true; break;
                case "wrongtable": row.TableName = "UserAnswers"; break;
            }
        });

        var problem = Assert.Single(problems);
        Assert.StartsWith($"index:{FirstCorrectUx}[", problem, StringComparison.Ordinal);
        Assert.Contains(expectedDefect, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void CriticalIndexSpecsMatchCompiledModel()
    {
        using var db = CreateOfflineContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        foreach (var spec in DatabaseSchemaVersionGuard.CriticalIndexSpecs)
        {
            var match = model.GetEntityTypes()
                .Where(e => e.GetTableName() == spec.Table)
                .SelectMany(e => e.GetIndexes().Select(i => (Entity: e, Index: i)))
                .Single(x => x.Index.GetDatabaseName() == spec.Name);
            var so = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(spec.Table, match.Entity.GetSchema());
            Assert.Equal(spec.Unique, match.Index.IsUnique);
            Assert.Equal(spec.Columns, string.Join(",", match.Index.Properties.Select(p => p.GetColumnName(so))));
            Assert.Equal(spec.Predicate is null, match.Index.GetFilter() is null);
        }
    }

    /// <summary>
    /// Real Postgres: a same-name UX index that is INVALID (left behind by a failed CREATE UNIQUE INDEX CONCURRENTLY)
    /// or wrongly defined must NOT satisfy the readiness guard. Gated like the other provider tests.
    /// </summary>
    [Fact]
    [Trait("Category", "PostgresProvider")]
    public async Task Guard_RejectsInvalidOrWronglyDefinedCriticalUniqueIndex_OnRealPostgres()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("POSTGRES_PROVIDER_VALIDATION_REQUIRED"), "1", StringComparison.Ordinal))
        {
            return;
        }

        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();
        await using var db = new ApiDbContext(database.CreateApiOptions());

        Assert.Empty(await DatabaseSchemaVersionGuard.FindMissingCriticalObjectsAsync(db, CancellationToken.None));

        // (a) wrong definition, same name: non-unique, no predicate.
        await db.Database.ExecuteSqlRawAsync($"DROP INDEX \"{FirstCorrectUx}\"");
        await db.Database.ExecuteSqlRawAsync($"CREATE INDEX \"{FirstCorrectUx}\" ON \"UserAnswerAudits\" (\"UserId\", \"QuestionId\", \"IsFirstTimeCorrect\")");
        var wrong = Assert.Single(await DatabaseSchemaVersionGuard.FindMissingCriticalObjectsAsync(db, CancellationToken.None));
        Assert.Contains("unique=false", wrong, StringComparison.Ordinal);
        Assert.Contains("predicate=<none>", wrong, StringComparison.Ordinal);

        // (b) invalid index, correct name AND definition: failed CONCURRENTLY build over duplicate first-correct rows.
        await db.Database.ExecuteSqlRawAsync($"DROP INDEX \"{FirstCorrectUx}\"");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"UserAnswerAudits\" (\"UserId\", \"QuestionId\", \"Answer\", \"IsCorrect\", \"AwardedXp\", \"AnsweredAt\", \"IsFirstTimeCorrect\") " +
            "VALUES ('dup-user', 1, 'a', true, 0, now(), true), ('dup-user', 1, 'a', true, 0, now(), true)");
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            $"CREATE UNIQUE INDEX CONCURRENTLY \"{FirstCorrectUx}\" ON \"UserAnswerAudits\" (\"UserId\", \"QuestionId\", \"IsFirstTimeCorrect\") WHERE \"IsFirstTimeCorrect\" = true"));
        var invalid = Assert.Single(await DatabaseSchemaVersionGuard.FindMissingCriticalObjectsAsync(db, CancellationToken.None));
        Assert.Contains("indisvalid=false", invalid, StringComparison.Ordinal);
        Assert.DoesNotContain("unique=", invalid, StringComparison.Ordinal);
        Assert.DoesNotContain("predicate=", invalid, StringComparison.Ordinal);
    }
}

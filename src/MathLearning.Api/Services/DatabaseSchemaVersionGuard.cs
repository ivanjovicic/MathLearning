using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MathLearning.Api.Services;

public enum DatabaseStartupMode
{
    AutoMigrate,
    ValidateExact,
    Skip
}

public sealed record DatabaseSchemaStatus(
    string Status,
    bool IsSchemaReady,
    string? LatestCodeMigration,
    string? LatestAppliedMigration,
    IReadOnlyList<string> PendingMigrations,
    IReadOnlyList<string> UnknownAppliedMigrations,
    string? FailureMessage,
    DateTime CheckedAtUtc)
{
    public int PendingMigrationsCount => PendingMigrations.Count;

    public int UnknownAppliedMigrationsCount => UnknownAppliedMigrations.Count;

    public static DatabaseSchemaStatus NotChecked { get; } = new(
        Status: "NotChecked",
        IsSchemaReady: false,
        LatestCodeMigration: null,
        LatestAppliedMigration: null,
        PendingMigrations: Array.Empty<string>(),
        UnknownAppliedMigrations: Array.Empty<string>(),
        FailureMessage: null,
        CheckedAtUtc: DateTime.MinValue);

    public static DatabaseSchemaStatus Failed(
        string failureMessage,
        string? latestCodeMigration = null,
        string? latestAppliedMigration = null,
        IReadOnlyList<string>? pendingMigrations = null,
        IReadOnlyList<string>? unknownAppliedMigrations = null)
        => new(
            Status: "Failed",
            IsSchemaReady: false,
            LatestCodeMigration: latestCodeMigration,
            LatestAppliedMigration: latestAppliedMigration,
            PendingMigrations: pendingMigrations ?? Array.Empty<string>(),
            UnknownAppliedMigrations: unknownAppliedMigrations ?? Array.Empty<string>(),
            FailureMessage: failureMessage,
            CheckedAtUtc: DateTime.UtcNow);
}

public sealed class DatabaseSchemaState
{
    private DatabaseSchemaStatus current = DatabaseSchemaStatus.NotChecked;

    public DatabaseSchemaStatus Current => current;

    public void Update(DatabaseSchemaStatus status)
    {
        current = status;
    }
}

public sealed class DatabaseSchemaVersionGuard
{
    private const string LegacyAdminIdentityAndDomainMigration =
        "20260118125140_InitIdentityAndDomain";

    private readonly ILogger<DatabaseSchemaVersionGuard> logger;

    public DatabaseSchemaVersionGuard(ILogger<DatabaseSchemaVersionGuard> logger)
    {
        this.logger = logger;
    }

    public static DatabaseStartupMode ResolveStartupMode(IHostEnvironment environment, IConfiguration configuration)
    {
        var configuredMode = configuration["Database:StartupMode"];
        if (!string.IsNullOrWhiteSpace(configuredMode) &&
            Enum.TryParse<DatabaseStartupMode>(configuredMode, ignoreCase: true, out var startupMode))
        {
            return startupMode;
        }

        if (environment.IsDevelopment())
        {
            return DatabaseStartupMode.AutoMigrate;
        }

        if (environment.IsEnvironment("Test"))
        {
            return DatabaseStartupMode.Skip;
        }

        return DatabaseStartupMode.ValidateExact;
    }

    public async Task<DatabaseSchemaStatus> CheckAsync(ApiDbContext db, CancellationToken cancellationToken = default)
    {
        var codeMigrations = db.Database.GetMigrations().ToArray();
        var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        var pendingMigrations = codeMigrations.Except(appliedMigrations, StringComparer.Ordinal).ToArray();
        var unknownAppliedMigrations = FilterUnknownAppliedMigrations(appliedMigrations, codeMigrations);
        var historyReady = pendingMigrations.Length == 0 && unknownAppliedMigrations.Length == 0;

        // Issue #47: migration history alone is not proof of physical schema (unregistered migration
        // classes were silently skipped). Verify critical quiz-settlement objects physically exist.
        var missingCriticalObjects = historyReady && db.Database.IsNpgsql()
            ? await FindMissingCriticalObjectsAsync(db, cancellationToken)
            : Array.Empty<string>();
        var isSchemaReady = historyReady && missingCriticalObjects.Length == 0;

        var status = new DatabaseSchemaStatus(
            Status: isSchemaReady ? "Ready" : "Mismatch",
            IsSchemaReady: isSchemaReady,
            LatestCodeMigration: codeMigrations.LastOrDefault(),
            LatestAppliedMigration: appliedMigrations.LastOrDefault(),
            PendingMigrations: pendingMigrations,
            UnknownAppliedMigrations: unknownAppliedMigrations,
            FailureMessage: isSchemaReady ? null : BuildMismatchSummary(pendingMigrations, unknownAppliedMigrations, missingCriticalObjects),
            CheckedAtUtc: DateTime.UtcNow);

        logger.LogInformation(
            "Database schema status evaluated. Status={Status} LatestCodeMigration={LatestCodeMigration} LatestAppliedMigration={LatestAppliedMigration} PendingCount={PendingCount} UnknownAppliedCount={UnknownAppliedCount}",
            status.Status,
            status.LatestCodeMigration ?? "<none>",
            status.LatestAppliedMigration ?? "<none>",
            status.PendingMigrationsCount,
            status.UnknownAppliedMigrationsCount);

        if (!isSchemaReady)
        {
            logger.LogWarning(
                "Database schema mismatch details. PendingMigrations={PendingMigrations}; UnknownAppliedMigrations={UnknownAppliedMigrations}",
                string.Join(", ", pendingMigrations),
                string.Join(", ", unknownAppliedMigrations));
        }

        return status;
    }

    /// <summary>
    /// Physical objects without which quiz answer settlement fails (42703 / missing ON CONFLICT target).
    /// Format: "column:Table.Column" or "index:IndexName".
    /// </summary>
    internal static readonly IReadOnlyList<string> CriticalPhysicalObjects = new[]
    {
        "column:UserAnswerAudits.ClientId",
        "column:UserAnswerAudits.CreatedAt",
        "column:UserAnswerAudits.IsFirstTimeCorrect",
        "column:UserAnswerAudits.IsOffline",
        "column:UserAnswerAudits.Reason",
        "column:UserAnswerAudits.Source",
        "column:UserAnswerAudits.TotalXpAfterAward",
        "index:UX_UserAnswerAudits_FirstCorrect_PerQuestion",
        "index:IX_UserAnswerAudits_User_Question_CreatedAt",
        "column:economy_transactions.OperationId",
        "index:UX_economy_transactions_user_type_operation",
    };

    /// <summary>Expected PostgreSQL shape for the critical audit columns whose drift can break quiz settlement.</summary>
    internal sealed record CriticalColumnSpec(
        string Table,
        string Name,
        string DataType,
        int? CharacterMaximumLength,
        bool Nullable);

    internal static readonly IReadOnlyList<CriticalColumnSpec> CriticalColumnSpecs = new[]
    {
        new CriticalColumnSpec("UserAnswerAudits", "ClientId", "character varying", 128, true),
        new CriticalColumnSpec("UserAnswerAudits", "CreatedAt", "timestamp with time zone", null, false),
        new CriticalColumnSpec("UserAnswerAudits", "IsFirstTimeCorrect", "boolean", null, false),
        new CriticalColumnSpec("UserAnswerAudits", "IsOffline", "boolean", null, false),
        new CriticalColumnSpec("UserAnswerAudits", "Reason", "character varying", 64, false),
        new CriticalColumnSpec("UserAnswerAudits", "Source", "character varying", 50, false),
        new CriticalColumnSpec("UserAnswerAudits", "TotalXpAfterAward", "integer", null, false),
    };

    /// <summary>Catalog facts for one critical column, as returned by <see cref="FindMissingCriticalObjectsAsync"/>.</summary>
    internal sealed class CriticalColumnCatalogRow
    {
        public string TableName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public int? CharacterMaximumLength { get; set; }
        public bool IsNullable { get; set; }
    }

    /// <summary>
    /// Semantic definition of each critical index. A same-name index is only accepted when it is
    /// valid + ready (pg_index.indisvalid / indisready), has the expected uniqueness, the exact ordered
    /// key columns, no expression keys, and the expected partial predicate (pg_get_expr normalised form).
    /// </summary>
    internal sealed record CriticalIndexSpec(string Name, string Table, bool Unique, string Columns, string? Predicate);

    internal static readonly IReadOnlyList<CriticalIndexSpec> CriticalIndexSpecs = new[]
    {
        new CriticalIndexSpec("UX_UserAnswerAudits_FirstCorrect_PerQuestion", "UserAnswerAudits", true,
            "UserId,QuestionId,IsFirstTimeCorrect", "(\"IsFirstTimeCorrect\" = true)"),
        new CriticalIndexSpec("IX_UserAnswerAudits_User_Question_CreatedAt", "UserAnswerAudits", false,
            "UserId,QuestionId,CreatedAt", null),
        new CriticalIndexSpec("UX_economy_transactions_user_type_operation", "economy_transactions", true,
            "UserId,TransactionType,OperationId", "(\"OperationId\" IS NOT NULL)"),
    };

    /// <summary>Catalog facts for one index (pg_index/pg_class), as returned by <see cref="FindMissingCriticalObjectsAsync"/>.</summary>
    internal sealed class CriticalIndexCatalogRow
    {
        public string IndexName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public bool IsUnique { get; set; }
        public bool IsValid { get; set; }
        public bool IsReady { get; set; }
        public bool HasExpressions { get; set; }
        public string? Columns { get; set; }
        public string? Predicate { get; set; }
    }

    internal static async Task<string[]> FindMissingCriticalObjectsAsync(ApiDbContext db, CancellationToken cancellationToken)
    {
        var columns = await db.Database.SqlQueryRaw<CriticalColumnCatalogRow>(
                """
                SELECT table_name AS "TableName",
                       column_name AS "ColumnName",
                       data_type AS "DataType",
                       character_maximum_length::integer AS "CharacterMaximumLength",
                       is_nullable = 'YES' AS "IsNullable"
                FROM information_schema.columns WHERE table_schema = current_schema()
                """)
            .ToListAsync(cancellationToken);
        var presentSet = columns
            .Select(column => $"column:{column.TableName}.{column.ColumnName}")
            .ToHashSet(StringComparer.Ordinal);

        // Names are compile-time constants (no user input); inlined because a string[] would be expanded into params.
        var nameList = string.Join(", ", CriticalIndexSpecs.Select(s => "'" + s.Name.Replace("'", "''") + "'"));
#pragma warning disable EF1002 // nameList is built only from compile-time constants above
        var indexRows = await db.Database.SqlQueryRaw<CriticalIndexCatalogRow>(
                $$"""
                SELECT ic.relname AS "IndexName", tc.relname AS "TableName",
                       i.indisunique AS "IsUnique", i.indisvalid AS "IsValid", i.indisready AS "IsReady",
                       (i.indexprs IS NOT NULL) AS "HasExpressions",
                       (SELECT string_agg(a.attname, ',' ORDER BY k.ord)
                        FROM unnest(i.indkey::int2[]) WITH ORDINALITY AS k(attnum, ord)
                        JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = k.attnum) AS "Columns",
                       pg_get_expr(i.indpred, i.indrelid) AS "Predicate"
                FROM pg_index i
                JOIN pg_class ic ON ic.oid = i.indexrelid
                JOIN pg_class tc ON tc.oid = i.indrelid
                JOIN pg_namespace n ON n.oid = ic.relnamespace
                WHERE n.nspname = current_schema() AND ic.relname IN ({{nameList}})
                """)
            .ToListAsync(cancellationToken);
#pragma warning restore EF1002

        var shapedColumnNames = CriticalColumnSpecs
            .Select(spec => $"column:{spec.Table}.{spec.Name}")
            .ToHashSet(StringComparer.Ordinal);
        var missingUnshapedColumns = CriticalPhysicalObjects
            .Where(o => o.StartsWith("column:", StringComparison.Ordinal)
                && !shapedColumnNames.Contains(o)
                && !presentSet.Contains(o));

        return EvaluateCriticalColumns(columns)
            .Concat(missingUnshapedColumns)
            .Concat(EvaluateCriticalIndexes(indexRows))
            .ToArray();
    }

    /// <summary>Pure evaluation of critical column catalog rows; returns one entry per missing or wrong-shaped column.</summary>
    internal static IEnumerable<string> EvaluateCriticalColumns(IEnumerable<CriticalColumnCatalogRow> rows)
    {
        var byColumn = rows.ToDictionary(row => (row.TableName, row.ColumnName));
        foreach (var spec in CriticalColumnSpecs)
        {
            if (!byColumn.TryGetValue((spec.Table, spec.Name), out var row))
            {
                yield return $"column:{spec.Table}.{spec.Name}[missing]";
                continue;
            }

            var defects = new List<string>();
            if (!string.Equals(row.DataType, spec.DataType, StringComparison.Ordinal))
            {
                defects.Add($"type={row.DataType}; expectedType={spec.DataType}");
            }

            if (row.CharacterMaximumLength != spec.CharacterMaximumLength)
            {
                defects.Add($"length={row.CharacterMaximumLength?.ToString() ?? "unbounded"}; expectedLength={spec.CharacterMaximumLength?.ToString() ?? "unbounded"}");
            }

            if (row.IsNullable != spec.Nullable)
            {
                defects.Add($"nullable={row.IsNullable.ToString().ToLowerInvariant()}; expectedNullable={spec.Nullable.ToString().ToLowerInvariant()}");
            }

            if (defects.Count > 0)
            {
                yield return $"column:{spec.Table}.{spec.Name}[{string.Join("; ", defects)}]";
            }
        }
    }

    /// <summary>Pure evaluation of catalog rows against <see cref="CriticalIndexSpecs"/>; returns one entry per defective index.</summary>
    internal static IEnumerable<string> EvaluateCriticalIndexes(IEnumerable<CriticalIndexCatalogRow> rows)
    {
        var byName = rows.ToDictionary(r => r.IndexName, StringComparer.Ordinal);
        foreach (var spec in CriticalIndexSpecs)
        {
            if (!byName.TryGetValue(spec.Name, out var row))
            {
                yield return $"index:{spec.Name}";
                continue;
            }

            var defects = new List<string>();
            if (!row.IsValid) defects.Add("indisvalid=false");
            if (!row.IsReady) defects.Add("indisready=false");
            if (!string.Equals(row.TableName, spec.Table, StringComparison.Ordinal)) defects.Add($"table={row.TableName}");
            if (row.IsUnique != spec.Unique) defects.Add($"unique={row.IsUnique.ToString().ToLowerInvariant()}");
            if (row.HasExpressions) defects.Add("expression keys");
            if (!string.Equals(row.Columns, spec.Columns, StringComparison.Ordinal)) defects.Add($"columns={row.Columns ?? "<none>"}");
            if (!string.Equals(row.Predicate, spec.Predicate, StringComparison.Ordinal)) defects.Add($"predicate={row.Predicate ?? "<none>"}");

            if (defects.Count > 0)
            {
                yield return $"index:{spec.Name}[{string.Join("; ", defects)}]";
            }
        }
    }

    internal static string[] FilterUnknownAppliedMigrations(
        IEnumerable<string> appliedMigrations,
        IEnumerable<string> codeMigrations)
    {
        var knownCodeMigrations = codeMigrations.ToHashSet(StringComparer.Ordinal);

        return appliedMigrations
            .Except(knownCodeMigrations, StringComparer.Ordinal)
            .Where(migration => !string.Equals(
                migration,
                LegacyAdminIdentityAndDomainMigration,
                StringComparison.Ordinal))
            .ToArray();
    }

    public InvalidOperationException CreateMismatchException(
        DatabaseStartupMode startupMode,
        string environmentName,
        DatabaseSchemaStatus status,
        Exception? innerException = null)
    {
        var guidance = startupMode == DatabaseStartupMode.AutoMigrate
            ? "Use scripts/db/drop-dev-db.ps1 for a local reset, or add an explicit repair migration before retrying startup."
            : "Apply the reviewed migration script before starting this service. Startup migrations are disabled in this environment.";

        var detailsSegment = string.IsNullOrWhiteSpace(status.FailureMessage)
            ? string.Empty
            : $" Details={status.FailureMessage};";

        var message = $"Database schema validation failed in {environmentName}. " +
            $"LatestApplied={status.LatestAppliedMigration ?? "<none>"}; " +
            $"LatestCode={status.LatestCodeMigration ?? "<none>"}; " +
            $"Pending={status.PendingMigrationsCount}; " +
            $"UnknownApplied={status.UnknownAppliedMigrationsCount};" +
            detailsSegment + " " +
            guidance;

        return new InvalidOperationException(message, innerException);
    }

    private static string BuildMismatchSummary(
        IReadOnlyCollection<string> pendingMigrations,
        IReadOnlyCollection<string> unknownAppliedMigrations,
        IReadOnlyCollection<string>? missingCriticalObjects = null)
    {
        var parts = new List<string>();

        if (missingCriticalObjects is { Count: > 0 })
        {
            parts.Add($"MissingCriticalObjects: {string.Join(", ", missingCriticalObjects)}");
        }

        if (pendingMigrations.Count > 0)
        {
            parts.Add($"Pending: {string.Join(", ", pendingMigrations)}");
        }

        if (unknownAppliedMigrations.Count > 0)
        {
            parts.Add($"UnknownApplied: {string.Join(", ", unknownAppliedMigrations)}");
        }

        return string.Join(" | ", parts);
    }
}

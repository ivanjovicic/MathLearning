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

    internal static async Task<string[]> FindMissingCriticalObjectsAsync(ApiDbContext db, CancellationToken cancellationToken)
    {
        var present = await db.Database.SqlQueryRaw<string>(
                """
                SELECT 'column:' || table_name || '.' || column_name AS "Value"
                FROM information_schema.columns WHERE table_schema = current_schema()
                UNION ALL
                SELECT 'index:' || indexname FROM pg_indexes WHERE schemaname = current_schema()
                """)
            .ToListAsync(cancellationToken);
        var presentSet = present.ToHashSet(StringComparer.Ordinal);
        return CriticalPhysicalObjects.Where(o => !presentSet.Contains(o)).ToArray();
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

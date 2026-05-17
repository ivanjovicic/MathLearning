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
        var unknownAppliedMigrations = appliedMigrations.Except(codeMigrations, StringComparer.Ordinal).ToArray();
        var isSchemaReady = pendingMigrations.Length == 0 && unknownAppliedMigrations.Length == 0;

        var status = new DatabaseSchemaStatus(
            Status: isSchemaReady ? "Ready" : "Mismatch",
            IsSchemaReady: isSchemaReady,
            LatestCodeMigration: codeMigrations.LastOrDefault(),
            LatestAppliedMigration: appliedMigrations.LastOrDefault(),
            PendingMigrations: pendingMigrations,
            UnknownAppliedMigrations: unknownAppliedMigrations,
            FailureMessage: isSchemaReady ? null : BuildMismatchSummary(pendingMigrations, unknownAppliedMigrations),
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
        IReadOnlyCollection<string> unknownAppliedMigrations)
    {
        var parts = new List<string>();

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

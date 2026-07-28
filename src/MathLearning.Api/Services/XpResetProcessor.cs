using System.Data;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace MathLearning.Api.Services;

public sealed record XpResetRunResult(
    string Status,
    bool SchemaReady,
    bool LockAcquired,
    int RowsAffected,
    TimeSpan Elapsed);

public sealed record XpResetWindow(
    DateTime UtcNow,
    DateTime DayStartUtc,
    DateTime WeekStartUtc,
    DateTime MonthStartUtc,
    bool ResetDaily,
    bool ResetWeekly,
    bool ResetMonthly)
{
    public static XpResetWindow Create(DateTime utcNow, DateTime? lastResetUtc = null)
    {
        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var dayStart = now.Date;
        var weekStart = GetWeekStart(now);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (!lastResetUtc.HasValue)
        {
            return new XpResetWindow(
                now,
                dayStart,
                weekStart,
                monthStart,
                ResetDaily: true,
                ResetWeekly: true,
                ResetMonthly: true);
        }

        var lastReset = DateTime.SpecifyKind(lastResetUtc.Value, DateTimeKind.Utc);
        return new XpResetWindow(
            now,
            dayStart,
            weekStart,
            monthStart,
            ResetDaily: lastReset.Date < dayStart,
            ResetWeekly: GetWeekStart(lastReset) < weekStart,
            ResetMonthly: lastReset.Year < now.Year || lastReset.Month < now.Month);
    }

    public static TimeSpan GetDelayUntilNextUtcBoundary(DateTimeOffset utcNow)
    {
        var now = utcNow.UtcDateTime;
        var nextBoundary = now.Date.AddDays(1);
        return nextBoundary - now;
    }

    private static DateTime GetWeekStart(DateTime utcDate)
    {
        var dayOfWeek = (int)utcDate.DayOfWeek;
        var daysToSubtract = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return utcDate.Date.AddDays(-daysToSubtract);
    }
}

public interface IXpResetOwnershipLease
{
    Task<bool> TryAcquireAsync(ApiDbContext db, CancellationToken cancellationToken);
}

public sealed class PostgresXpResetOwnershipLease : IXpResetOwnershipLease
{
    private const string LeaseKey = "xp-reset-periods";

    public async Task<bool> TryAcquireAsync(ApiDbContext db, CancellationToken cancellationToken)
    {
        if (!(db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return true;
        }

        var currentTransaction = db.Database.CurrentTransaction?.GetDbTransaction();
        if (currentTransaction is null)
        {
            throw new InvalidOperationException("XP reset lease requires an open transaction.");
        }

        var connection = db.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.Transaction = currentTransaction;
        command.CommandText = "SELECT pg_try_advisory_xact_lock(hashtext(@lease_key));";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@lease_key";
        parameter.Value = LeaseKey;
        command.Parameters.Add(parameter);

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool acquired && acquired;
    }
}

public sealed class XpResetProcessor
{
    private const string ResetLockStatus = "locked";
    private const string ResetSkippedStatus = "skipped";
    private const string ResetAppliedStatus = "applied";
    private const string ResetSchemaNotReadyStatus = "schema-not-ready";

    private readonly ApiDbContext db;
    private readonly DatabaseSchemaState schemaState;
    private readonly IXpResetOwnershipLease lease;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<XpResetProcessor> logger;

    public XpResetProcessor(
        ApiDbContext db,
        DatabaseSchemaState schemaState,
        IXpResetOwnershipLease lease,
        TimeProvider timeProvider,
        ILogger<XpResetProcessor> logger)
    {
        this.db = db;
        this.schemaState = schemaState;
        this.lease = lease;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task<XpResetRunResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();

        if (!schemaState.Current.IsSchemaReady)
        {
            logger.LogWarning(
                "Skipping XP reset because schema is not ready. Status={SchemaStatus} Failure={Failure}",
                schemaState.Current.Status,
                schemaState.Current.FailureMessage ?? "<none>");

            return new XpResetRunResult(
                ResetSchemaNotReadyStatus,
                SchemaReady: false,
                LockAcquired: false,
                RowsAffected: 0,
                Elapsed: timeProvider.GetUtcNow() - startedAt);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var lockAcquired = await lease.TryAcquireAsync(db, cancellationToken);
        if (!lockAcquired)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogInformation("Skipping XP reset because another replica owns the advisory lock.");

            return new XpResetRunResult(
                ResetSkippedStatus,
                SchemaReady: true,
                LockAcquired: false,
                RowsAffected: 0,
                Elapsed: timeProvider.GetUtcNow() - startedAt);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var window = XpResetWindow.Create(now);
        var parameters = new object[]
        {
            new NpgsqlParameter("@now", now),
            new NpgsqlParameter("@today", window.DayStartUtc),
            new NpgsqlParameter("@weekStart", window.WeekStartUtc),
            new NpgsqlParameter("@monthStart", window.MonthStartUtc)
        };

        const string updateSql = """
UPDATE "UserProfiles"
SET "DailyXp" = CASE
        WHEN "LastXpResetDate" IS NULL OR "LastXpResetDate" < @today THEN 0
        ELSE "DailyXp"
    END,
    "WeeklyXp" = CASE
        WHEN "LastXpResetDate" IS NULL OR "LastXpResetDate" < @weekStart THEN 0
        ELSE "WeeklyXp"
    END,
    "MonthlyXp" = CASE
        WHEN "LastXpResetDate" IS NULL OR "LastXpResetDate" < @monthStart THEN 0
        ELSE "MonthlyXp"
    END,
    "LastXpResetDate" = CASE
        WHEN "LastXpResetDate" IS NULL
          OR "LastXpResetDate" < @today
          OR "LastXpResetDate" < @weekStart
          OR "LastXpResetDate" < @monthStart THEN @now
        ELSE "LastXpResetDate"
    END,
    "UpdatedAt" = CASE
        WHEN "LastXpResetDate" IS NULL
          OR "LastXpResetDate" < @today
          OR "LastXpResetDate" < @weekStart
          OR "LastXpResetDate" < @monthStart THEN @now
        ELSE "UpdatedAt"
    END
WHERE "LastXpResetDate" IS NULL
   OR "LastXpResetDate" < @today
   OR "LastXpResetDate" < @weekStart
   OR "LastXpResetDate" < @monthStart;
""";

        var rowsAffected = await db.Database.ExecuteSqlRawAsync(updateSql, parameters, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "XP reset applied. RowsAffected={RowsAffected} DayStartUtc={DayStartUtc:O} WeekStartUtc={WeekStartUtc:O} MonthStartUtc={MonthStartUtc:O} ElapsedMs={ElapsedMs:0.00}",
            rowsAffected,
            window.DayStartUtc,
            window.WeekStartUtc,
            window.MonthStartUtc,
            (timeProvider.GetUtcNow() - startedAt).TotalMilliseconds);

        return new XpResetRunResult(
            ResetAppliedStatus,
            SchemaReady: true,
            LockAcquired: true,
            RowsAffected: rowsAffected,
            Elapsed: timeProvider.GetUtcNow() - startedAt);
    }
}

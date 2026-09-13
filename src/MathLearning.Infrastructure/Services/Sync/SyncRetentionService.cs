using System.Diagnostics;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MathLearning.Infrastructure.Services.Sync;

public sealed record SyncRetentionCleanupResult(
    int DeletedSyncEventLogs,
    int DeletedServerSyncEvents,
    int DeletedSyncDeadLetters,
    TimeSpan Duration);

public sealed class SyncRetentionService
{
    private readonly ApiDbContext db;
    private readonly IOptions<SyncOptions> options;
    private readonly SyncMetricsService metrics;
    private readonly ILogger<SyncRetentionService> logger;

    public SyncRetentionService(
        ApiDbContext db,
        IOptions<SyncOptions> options,
        SyncMetricsService metrics,
        ILogger<SyncRetentionService> logger)
    {
        this.db = db;
        this.options = options;
        this.metrics = metrics;
        this.logger = logger;
    }

    public async Task<SyncRetentionCleanupResult> CleanupAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var batchSize = Math.Clamp(options.Value.RetentionBatchSize, 1, 5000);
        var sw = Stopwatch.StartNew();

        var deletedEventLogs = await DeleteEventLogBatchAsync(batchSize, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var deletedServerEvents = await DeleteServerEventBatchAsync(batchSize, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var deletedDeadLetters = await DeleteDeadLetterBatchAsync(batchSize, cancellationToken);
        sw.Stop();

        var result = new SyncRetentionCleanupResult(
            deletedEventLogs,
            deletedServerEvents,
            deletedDeadLetters,
            sw.Elapsed);

        metrics.RecordRetentionCleanup("SyncEventLog", deletedEventLogs, sw.Elapsed);
        metrics.RecordRetentionCleanup("ServerSyncEvent", deletedServerEvents, sw.Elapsed);
        metrics.RecordRetentionCleanup("SyncDeadLetter", deletedDeadLetters, sw.Elapsed);

        if (deletedEventLogs > 0 || deletedServerEvents > 0 || deletedDeadLetters > 0)
        {
            logger.LogInformation(
                "Sync retention cleanup completed. EventLogs={EventLogs} ServerEvents={ServerEvents} DeadLetters={DeadLetters} DurationMs={DurationMs}",
                deletedEventLogs,
                deletedServerEvents,
                deletedDeadLetters,
                (long)sw.Elapsed.TotalMilliseconds);
        }

        return result;
    }

    private async Task<int> DeleteEventLogBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, options.Value.SyncEventLogRetentionDays));
        var query = db.SyncEventLogs
            .AsNoTracking()
            .Where(x => x.ReceivedAtUtc <= cutoff &&
                        (x.Status == SyncEventStatuses.Processed || x.Status == SyncEventStatuses.Rejected))
            .OrderBy(x => x.Status)
            .ThenBy(x => x.ReceivedAtUtc)
            .ThenBy(x => x.Id)
            .Take(batchSize);

        if (db.Database.IsRelational())
        {
            var ids = await query.Select(x => x.Id).ToListAsync(cancellationToken);
            if (ids.Count == 0)
            {
                return 0;
            }

            return await db.SyncEventLogs
                .Where(x => ids.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        var rows = await query.ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        db.SyncEventLogs.RemoveRange(rows);
        return await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> DeleteServerEventBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, options.Value.ServerSyncEventRetentionDays));
        var query = db.ServerSyncEvents
            .AsNoTracking()
            .Where(x => x.CreatedAtUtc <= cutoff)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Take(batchSize);

        if (db.Database.IsRelational())
        {
            var ids = await query.Select(x => x.Id).ToListAsync(cancellationToken);
            if (ids.Count == 0)
            {
                return 0;
            }

            return await db.ServerSyncEvents
                .Where(x => ids.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        var rows = await query.ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        db.ServerSyncEvents.RemoveRange(rows);
        return await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> DeleteDeadLetterBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, options.Value.SyncDeadLetterRetentionDays));
        var query = db.SyncDeadLetters
            .AsNoTracking()
            .Where(x => (x.Status == SyncDeadLetterStatuses.Resolved || x.Status == SyncDeadLetterStatuses.Exhausted) &&
                        x.LastFailedAtUtc <= cutoff)
            .OrderBy(x => x.Status)
            .ThenBy(x => x.LastFailedAtUtc)
            .ThenBy(x => x.Id)
            .Take(batchSize);

        if (db.Database.IsRelational())
        {
            var ids = await query.Select(x => x.Id).ToListAsync(cancellationToken);
            if (ids.Count == 0)
            {
                return 0;
            }

            return await db.SyncDeadLetters
                .Where(x => ids.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        var rows = await query.ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        db.SyncDeadLetters.RemoveRange(rows);
        return await db.SaveChangesAsync(cancellationToken);
    }
}

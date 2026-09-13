using System.Collections.Concurrent;
using MathLearning.Application.DTOs.Sync;

namespace MathLearning.Infrastructure.Services.Sync;

public sealed class SyncMetricsService
{
    private long syncRequests;
    private long processedOperations;
    private long duplicateOperations;
    private long rejectedOperations;
    private long failedOperations;
    private long deadLetterOperations;
    private long retentionCleanupRuns;
    private long retentionDeletedRows;
    private long retentionDurationMilliseconds;
    private readonly ConcurrentDictionary<string, long> failuresByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> payloadSizeBuckets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> retentionDeletedRowsByTable = new(StringComparer.OrdinalIgnoreCase);

    public void IncrementSyncRequests() => Interlocked.Increment(ref syncRequests);
    public void IncrementProcessed() => Interlocked.Increment(ref processedOperations);
    public void IncrementDuplicate() => Interlocked.Increment(ref duplicateOperations);

    public void IncrementRejected(string code)
    {
        Interlocked.Increment(ref rejectedOperations);
        failuresByCode.AddOrUpdate(code, 1, static (_, value) => value + 1);
    }

    public void IncrementFailed(string code)
    {
        Interlocked.Increment(ref failedOperations);
        failuresByCode.AddOrUpdate(code, 1, static (_, value) => value + 1);
    }

    public void IncrementDeadLetter(string code)
    {
        Interlocked.Increment(ref deadLetterOperations);
        failuresByCode.AddOrUpdate(code, 1, static (_, value) => value + 1);
    }

    public void RecordPayloadSize(int payloadBytes)
    {
        var bucket = payloadBytes switch
        {
            <= 1024 => "0-1KB",
            <= 4 * 1024 => "1-4KB",
            <= 16 * 1024 => "4-16KB",
            <= 64 * 1024 => "16-64KB",
            _ => "64KB+"
        };

        payloadSizeBuckets.AddOrUpdate(bucket, 1, static (_, value) => value + 1);
    }

    public void RecordRetentionCleanup(string tableName, int deletedRows, TimeSpan duration)
    {
        Interlocked.Increment(ref retentionCleanupRuns);
        Interlocked.Add(ref retentionDeletedRows, deletedRows);
        Interlocked.Add(ref retentionDurationMilliseconds, (long)Math.Max(0, duration.TotalMilliseconds));

        if (deletedRows > 0)
        {
            retentionDeletedRowsByTable.AddOrUpdate(tableName, deletedRows, (_, value) => value + deletedRows);
        }
    }

    public SyncMetricsSnapshotDto Snapshot() => new(
        Interlocked.Read(ref syncRequests),
        Interlocked.Read(ref processedOperations),
        Interlocked.Read(ref duplicateOperations),
        Interlocked.Read(ref rejectedOperations),
        Interlocked.Read(ref failedOperations),
        Interlocked.Read(ref deadLetterOperations),
        Interlocked.Read(ref retentionCleanupRuns),
        Interlocked.Read(ref retentionDeletedRows),
        Interlocked.Read(ref retentionDurationMilliseconds),
        failuresByCode.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value),
        payloadSizeBuckets.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value),
        retentionDeletedRowsByTable.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value));
}

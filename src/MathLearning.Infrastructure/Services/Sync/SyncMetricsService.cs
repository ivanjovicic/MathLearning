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
    private readonly ConcurrentDictionary<string, long> failuresByCode = new(StringComparer.OrdinalIgnoreCase);

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

    public SyncMetricsSnapshotDto Snapshot() => new(
        Interlocked.Read(ref syncRequests),
        Interlocked.Read(ref processedOperations),
        Interlocked.Read(ref duplicateOperations),
        Interlocked.Read(ref rejectedOperations),
        Interlocked.Read(ref failedOperations),
        Interlocked.Read(ref deadLetterOperations),
        failuresByCode.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value));
}

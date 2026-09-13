namespace MathLearning.Api.Services;

public sealed record ExplanationCacheMetricsSnapshot(
    long HitCount,
    long MissCount,
    long StampedeSuppressedCount,
    long GenerationCount,
    double AverageGenerationDurationMs,
    long WriteFailureCount,
    long CleanupRuns,
    long ExpiredRowsDeleted,
    long OversizedPayloadSkips);

public sealed class ExplanationCacheMetrics
{
    private long hitCount;
    private long missCount;
    private long stampedeSuppressedCount;
    private long generationCount;
    private long generationDurationMsTotal;
    private long writeFailureCount;
    private long cleanupRuns;
    private long expiredRowsDeleted;
    private long oversizedPayloadSkips;

    public void RecordHit() => Interlocked.Increment(ref hitCount);

    public void RecordMiss() => Interlocked.Increment(ref missCount);

    public void RecordStampedeSuppressed() => Interlocked.Increment(ref stampedeSuppressedCount);

    public void RecordGeneration(TimeSpan duration)
    {
        Interlocked.Increment(ref generationCount);
        Interlocked.Add(ref generationDurationMsTotal, (long)Math.Round(duration.TotalMilliseconds));
    }

    public void RecordWriteFailure() => Interlocked.Increment(ref writeFailureCount);

    public void RecordCleanup(int expiredRows)
    {
        Interlocked.Increment(ref cleanupRuns);
        if (expiredRows > 0)
            Interlocked.Add(ref expiredRowsDeleted, expiredRows);
    }

    public void RecordOversizedPayloadSkip() => Interlocked.Increment(ref oversizedPayloadSkips);

    public ExplanationCacheMetricsSnapshot GetSnapshot()
    {
        var generations = Interlocked.Read(ref generationCount);
        var durationTotal = Interlocked.Read(ref generationDurationMsTotal);
        return new ExplanationCacheMetricsSnapshot(
            Interlocked.Read(ref hitCount),
            Interlocked.Read(ref missCount),
            Interlocked.Read(ref stampedeSuppressedCount),
            generations,
            generations == 0 ? 0 : (double)durationTotal / generations,
            Interlocked.Read(ref writeFailureCount),
            Interlocked.Read(ref cleanupRuns),
            Interlocked.Read(ref expiredRowsDeleted),
            Interlocked.Read(ref oversizedPayloadSkips));
    }
}

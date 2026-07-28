using System.Collections.Concurrent;

namespace MathLearning.Api.Middleware;

public sealed record RateLimitStoreSnapshot(
    int PartitionCount,
    long AllowedRequests,
    long RejectedRequests,
    long SaturationRejections,
    long EvictedPartitions,
    long CleanupRuns);

public sealed class InMemoryRateLimitCounterStore : IRateLimitCounterStore
{
    private const int DefaultMaxPartitions = 100_000;

    private readonly ConcurrentDictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly object _creationGate = new();
    private long _lastCleanupUtcTicks;
    private long _allowedRequests;
    private long _rejectedRequests;
    private long _saturationRejections;
    private long _evictedPartitions;
    private long _cleanupRuns;

    public InMemoryRateLimitCounterStore()
        : this(TimeProvider.System)
    {
    }

    public InMemoryRateLimitCounterStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool TryAcquire(
        string key,
        int limit,
        TimeSpan window,
        out int retryAfterSeconds,
        int maxPartitions = DefaultMaxPartitions)
    {
        ValidateConfiguration(limit, window, maxPartitions);

        var now = _timeProvider.GetUtcNow();
        SweepExpiredPartitionsIfNeeded(now, window, maxPartitions);

        if (!_buckets.TryGetValue(key, out var bucket))
        {
            lock (_creationGate)
            {
                if (_buckets.TryGetValue(key, out bucket))
                    goto bucketReady;

                if (_buckets.Count >= maxPartitions)
                {
                    Interlocked.Increment(ref _saturationRejections);
                    retryAfterSeconds = GetRetryAfterSeconds(window);
                    return false;
                }

                bucket = new Bucket(now.ToUnixTimeMilliseconds());
                _buckets[key] = bucket;
            }
        }

    bucketReady:
        lock (bucket.Gate)
        {
            var nowUnixMs = now.ToUnixTimeMilliseconds();
            bucket.LastAccessUtcMs = nowUnixMs;
            TrimExpired(bucket, nowUnixMs, window);

            if (bucket.Timestamps.Count >= limit)
            {
                Interlocked.Increment(ref _rejectedRequests);
                retryAfterSeconds = GetRetryAfterSeconds(bucket.Timestamps.Peek(), nowUnixMs, window);
                return false;
            }

            bucket.Timestamps.Enqueue(nowUnixMs);
            Interlocked.Increment(ref _allowedRequests);
            retryAfterSeconds = 0;
            return true;
        }
    }

    public RateLimitStoreSnapshot GetSnapshot() =>
        new(
            PartitionCount: _buckets.Count,
            AllowedRequests: Interlocked.Read(ref _allowedRequests),
            RejectedRequests: Interlocked.Read(ref _rejectedRequests),
            SaturationRejections: Interlocked.Read(ref _saturationRejections),
            EvictedPartitions: Interlocked.Read(ref _evictedPartitions),
            CleanupRuns: Interlocked.Read(ref _cleanupRuns));

    private void SweepExpiredPartitionsIfNeeded(DateTimeOffset now, TimeSpan window, int maxPartitions)
    {
        var lastCleanup = Volatile.Read(ref _lastCleanupUtcTicks);
        var cleanupIntervalTicks = window.Ticks;
        if (cleanupIntervalTicks <= 0)
            return;

        var nowUnixMs = now.ToUnixTimeMilliseconds();
        var nowTicks = now.UtcDateTime.Ticks;
        if (nowTicks - lastCleanup < cleanupIntervalTicks && _buckets.Count <= maxPartitions)
            return;

        if (Interlocked.CompareExchange(ref _lastCleanupUtcTicks, nowTicks, lastCleanup) != lastCleanup)
            return;

        Interlocked.Increment(ref _cleanupRuns);

        foreach (var entry in _buckets)
        {
            var bucket = entry.Value;
            lock (bucket.Gate)
            {
                TrimExpired(bucket, nowUnixMs, window);

                if (bucket.Timestamps.Count > 0)
                    continue;

                if (nowUnixMs - bucket.LastAccessUtcMs < window.TotalMilliseconds)
                    continue;

                if (_buckets.TryGetValue(entry.Key, out var current) && ReferenceEquals(current, bucket) && _buckets.TryRemove(entry.Key, out _))
                {
                    Interlocked.Increment(ref _evictedPartitions);
                }
            }
        }
    }

    private static void TrimExpired(Bucket bucket, long nowUnixMs, TimeSpan window)
    {
        var windowStart = nowUnixMs - (long)window.TotalMilliseconds;

        while (bucket.Timestamps.TryPeek(out var oldest) && oldest < windowStart)
            bucket.Timestamps.TryDequeue(out _);
    }

    private static int GetRetryAfterSeconds(long oldestAcceptedUnixMs, long nowUnixMs, TimeSpan window)
    {
        var remainingMs = oldestAcceptedUnixMs + (long)window.TotalMilliseconds - nowUnixMs;
        if (remainingMs <= 0)
            return 1;

        return (int)Math.Ceiling(remainingMs / 1000d);
    }

    private static int GetRetryAfterSeconds(TimeSpan window)
    {
        var seconds = (int)Math.Ceiling(window.TotalSeconds);
        return Math.Max(1, seconds);
    }

    private static void ValidateConfiguration(int limit, TimeSpan window, int maxPartitions)
    {
        if (limit <= 0 || limit > 10_000)
            throw new InvalidOperationException("RateLimiting:Sliding:Limit must be between 1 and 10000.");

        if (window <= TimeSpan.Zero || window > TimeSpan.FromHours(24))
            throw new InvalidOperationException("RateLimiting:Sliding:WindowSeconds must be between 1 and 86400.");

        if (maxPartitions < limit || maxPartitions > 1_000_000)
            throw new InvalidOperationException("RateLimiting:Sliding:MaxPartitions must be between the limit and 1000000.");
    }

    private sealed class Bucket
    {
        public Bucket(long createdAtUtcMs)
        {
            LastAccessUtcMs = createdAtUtcMs;
        }

        public object Gate { get; } = new();

        public Queue<long> Timestamps { get; } = new();

        public long LastAccessUtcMs { get; set; }
    }
}

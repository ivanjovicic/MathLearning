using System.Collections.Concurrent;
using MathLearning.Api.Middleware;

namespace MathLearning.Tests.Middleware;

public sealed class InMemoryRateLimitCounterStoreTests
{
    [Fact]
    public async Task TryAcquire_AllowsExactConcurrentBoundary_OnSingleKey()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 7, 28, 0, 0, 0, TimeSpan.Zero));
        var store = new InMemoryRateLimitCounterStore(clock);
        const int limit = 8;
        const int totalRequests = 32;

        var startGate = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, totalRequests)
            .Select(_ => Task.Run(() =>
            {
                startGate.Wait();
                return store.TryAcquire("user:shared", limit, TimeSpan.FromMinutes(1), out _);
            }))
            .ToArray();

        startGate.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(limit, results.Count(x => x));
        Assert.Equal(totalRequests - limit, results.Count(x => !x));

        var snapshot = store.GetSnapshot();
        Assert.Equal(1, snapshot.PartitionCount);
        Assert.Equal(limit, snapshot.AllowedRequests);
        Assert.Equal(totalRequests - limit, snapshot.RejectedRequests);
        Assert.Equal(0, snapshot.SaturationRejections);
    }

    [Fact]
    public void TryAcquire_ExpandsAndThenEvictsExpiredPartitions()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 7, 28, 0, 0, 0, TimeSpan.Zero));
        var store = new InMemoryRateLimitCounterStore(clock);
        var window = TimeSpan.FromSeconds(10);

        for (var i = 0; i < 100_000; i++)
        {
            var key = $"ip:{i}";
            Assert.True(store.TryAcquire(key, 1, window, out _, maxPartitions: 100_000));
        }

        var beforeCleanup = store.GetSnapshot();
        Assert.Equal(100_000, beforeCleanup.PartitionCount);
        Assert.Equal(100_000, beforeCleanup.AllowedRequests);
        Assert.Equal(0, beforeCleanup.RejectedRequests);

        clock.Advance(TimeSpan.FromSeconds(11));

        Assert.True(store.TryAcquire("ip:fresh", 1, window, out _, maxPartitions: 100_000));

        var afterCleanup = store.GetSnapshot();
        Assert.Equal(1, afterCleanup.PartitionCount);
        Assert.Equal(100_000, afterCleanup.EvictedPartitions);
        Assert.True(afterCleanup.CleanupRuns >= 1);
    }

    [Fact]
    public void TryAcquire_SustainedRejectedTraffic_RemainsBounded()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 7, 28, 0, 0, 0, TimeSpan.Zero));
        var store = new InMemoryRateLimitCounterStore(clock);
        var window = TimeSpan.FromMinutes(1);

        Assert.True(store.TryAcquire("ip:bounded", 1, window, out _));

        for (var i = 0; i < 5_000; i++)
        {
            Assert.False(store.TryAcquire("ip:bounded", 1, window, out var retryAfter));
            Assert.Equal(60, retryAfter);
        }

        var snapshot = store.GetSnapshot();
        Assert.Equal(1, snapshot.PartitionCount);
        Assert.Equal(1, snapshot.AllowedRequests);
        Assert.Equal(5_000, snapshot.RejectedRequests);
        Assert.Equal(0, snapshot.SaturationRejections);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow;

        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan delta) => utcNow = utcNow.Add(delta);
    }
}

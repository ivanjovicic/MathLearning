using System.Collections.Concurrent;

namespace MathLearning.Api.Middleware;

public sealed class InMemoryRateLimitCounterStore : IRateLimitCounterStore
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<long>> _counters = new();

    public bool TryAcquire(string key, int limit, TimeSpan window, out int retryAfterSeconds)
    {
        retryAfterSeconds = (int)Math.Ceiling(window.TotalSeconds);

        var q = _counters.GetOrAdd(key, _ => new ConcurrentQueue<long>());
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)window.TotalMilliseconds;

        q.Enqueue(now);

        while (q.TryPeek(out var ts) && ts < windowStart)
            q.TryDequeue(out _);

        return q.Count <= limit;
    }
}

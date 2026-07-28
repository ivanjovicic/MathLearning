namespace MathLearning.Api.Middleware;

public interface IRateLimitCounterStore
{
    bool TryAcquire(
        string key,
        int limit,
        TimeSpan window,
        out int retryAfterSeconds,
        int maxPartitions = 100_000);

    RateLimitStoreSnapshot GetSnapshot();
}

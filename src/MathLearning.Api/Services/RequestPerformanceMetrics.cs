using System.Threading;

namespace MathLearning.Api.Services;

public sealed record RequestPerformanceMetricsSnapshot(
    long TotalRequests,
    long EmittedRequests,
    long SlowRequests,
    long QueryBudgetViolations,
    long SampledRequests,
    long ErrorRequests,
    double AverageElapsedMs,
    double AverageQueryCount);

public sealed class RequestPerformanceMetrics
{
    private long totalRequests;
    private long emittedRequests;
    private long slowRequests;
    private long queryBudgetViolations;
    private long sampledRequests;
    private long errorRequests;
    private long elapsedMsTotal;
    private long queryCountTotal;

    public void Record(bool emitted, string? reason, double elapsedMs, int queryCount)
    {
        Interlocked.Increment(ref totalRequests);
        if (emitted)
            Interlocked.Increment(ref emittedRequests);

        if (elapsedMs >= 0)
            Interlocked.Add(ref elapsedMsTotal, (long)Math.Round(elapsedMs));

        if (queryCount >= 0)
            Interlocked.Add(ref queryCountTotal, queryCount);

        switch (Normalize(reason))
        {
            case "slow_request":
                Interlocked.Increment(ref slowRequests);
                break;
            case "query_budget":
                Interlocked.Increment(ref queryBudgetViolations);
                break;
            case "sampled":
                Interlocked.Increment(ref sampledRequests);
                break;
            case "error":
                Interlocked.Increment(ref errorRequests);
                break;
        }
    }

    public RequestPerformanceMetricsSnapshot Snapshot()
    {
        var requests = Interlocked.Read(ref totalRequests);
        return new RequestPerformanceMetricsSnapshot(
            requests,
            Interlocked.Read(ref emittedRequests),
            Interlocked.Read(ref slowRequests),
            Interlocked.Read(ref queryBudgetViolations),
            Interlocked.Read(ref sampledRequests),
            Interlocked.Read(ref errorRequests),
            requests == 0 ? 0 : (double)Interlocked.Read(ref elapsedMsTotal) / requests,
            requests == 0 ? 0 : (double)Interlocked.Read(ref queryCountTotal) / requests);
    }

    private static string Normalize(string? reason)
        => string.IsNullOrWhiteSpace(reason) ? "unknown" : reason.Trim().ToLowerInvariant();
}

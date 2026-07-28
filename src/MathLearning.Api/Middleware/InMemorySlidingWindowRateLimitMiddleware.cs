using MathLearning.Application.DTOs.Common;
using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace MathLearning.Api.Middleware;

public class InMemorySlidingWindowRateLimitMiddleware
{
    private const int DefaultLimit = 100;
    private const int DefaultWindowSeconds = 60;
    private const int DefaultMaxPartitions = 100_000;

    private readonly RequestDelegate _next;
    private readonly IRateLimitCounterStore _store;
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly int _maxPartitions;

    public InMemorySlidingWindowRateLimitMiddleware(
        RequestDelegate next,
        IConfiguration cfg,
        IRateLimitCounterStore store)
    {
        _next = next;
        _store = store;
        _limit = cfg.GetValue<int?>("RateLimiting:Sliding:Limit") ?? DefaultLimit;
        var windowSeconds = cfg.GetValue<int?>("RateLimiting:Sliding:WindowSeconds") ?? DefaultWindowSeconds;
        _maxPartitions = cfg.GetValue<int?>("RateLimiting:Sliding:MaxPartitions") ?? DefaultMaxPartitions;

        ValidateConfiguration(_limit, windowSeconds, _maxPartitions);
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    public async Task Invoke(HttpContext context)
    {
        if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var key = RateLimitClientIdentity.Resolve(context);

        if (!_store.TryAcquire(key, _limit, _window, out var retryAfterSeconds, _maxPartitions))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

            var result = ApiResult<object>.RateLimited(
                error: "Too many requests (sliding window, in-memory).",
                errorDetails: new
                {
                    limit = _limit,
                    windowSeconds = (int)Math.Ceiling(_window.TotalSeconds),
                    maxPartitions = _maxPartitions
                },
                traceId: context.TraceIdentifier,
                retryAfterSeconds: retryAfterSeconds);

            await context.Response.WriteAsJsonAsync(result);
            return;
        }

        await _next(context);
    }

    private static void ValidateConfiguration(int limit, int windowSeconds, int maxPartitions)
    {
        if (limit <= 0 || limit > 10_000)
            throw new InvalidOperationException("RateLimiting:Sliding:Limit must be between 1 and 10000.");

        if (windowSeconds <= 0 || windowSeconds > 86_400)
            throw new InvalidOperationException("RateLimiting:Sliding:WindowSeconds must be between 1 and 86400.");

        if (maxPartitions < limit || maxPartitions > 1_000_000)
            throw new InvalidOperationException("RateLimiting:Sliding:MaxPartitions must be between the limit and 1000000.");
    }
}

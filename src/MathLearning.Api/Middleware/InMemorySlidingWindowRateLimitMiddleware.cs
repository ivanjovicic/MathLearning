using System.Collections.Concurrent;
using MathLearning.Application.DTOs.Common;
using Microsoft.AspNetCore.Http;

namespace MathLearning.Api.Middleware;

public class InMemorySlidingWindowRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly int _limit;
    private readonly TimeSpan _window;

    // per-IP sliding windows stored in memory (single-node)
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<long>> _counters = new();

    public InMemorySlidingWindowRateLimitMiddleware(RequestDelegate next, IConfiguration cfg)
    {
        _next = next;
        _limit = cfg.GetValue<int?>("RateLimiting:Sliding:Limit") ?? 100;
        var windowSeconds = cfg.GetValue<int?>("RateLimiting:Sliding:WindowSeconds") ?? 60;
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    public async Task Invoke(HttpContext context)
    {
        // Never rate-limit health checks.
        if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var q = _counters.GetOrAdd(ip, _ => new ConcurrentQueue<long>());

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)_window.TotalMilliseconds;

        q.Enqueue(now);

        // prune old timestamps
        while (q.TryPeek(out var ts) && ts < windowStart)
            q.TryDequeue(out _);

        if (q.Count > _limit)
        {
            var retryAfterSeconds = (int)Math.Ceiling(_window.TotalSeconds);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            context.Response.Headers.RetryAfter = retryAfterSeconds.ToString();

            var result = ApiResult<object>.RateLimited(
                error: "Too many requests (sliding window, in-memory).",
                errorDetails: new
                {
                    limit = _limit,
                    windowSeconds = retryAfterSeconds
                },
                traceId: context.TraceIdentifier,
                retryAfterSeconds: retryAfterSeconds);

            await context.Response.WriteAsJsonAsync(result);
            return;
        }

        await _next(context);
    }
}

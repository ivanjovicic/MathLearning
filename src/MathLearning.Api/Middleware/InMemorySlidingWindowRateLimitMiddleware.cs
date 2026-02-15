using System.Collections.Concurrent;
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
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = _window.TotalSeconds.ToString();
            await context.Response.WriteAsync("Too many requests (sliding window, in-memory).");
            return;
        }

        await _next(context);
    }
}

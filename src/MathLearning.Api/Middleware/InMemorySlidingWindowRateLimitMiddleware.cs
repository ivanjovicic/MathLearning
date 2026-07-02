using MathLearning.Application.DTOs.Common;
using Microsoft.AspNetCore.Http;

namespace MathLearning.Api.Middleware;

public class InMemorySlidingWindowRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitCounterStore _store;
    private readonly int _limit;
    private readonly TimeSpan _window;

    public InMemorySlidingWindowRateLimitMiddleware(
        RequestDelegate next,
        IConfiguration cfg,
        IRateLimitCounterStore store)
    {
        _next = next;
        _store = store;
        _limit = cfg.GetValue<int?>("RateLimiting:Sliding:Limit") ?? 100;
        var windowSeconds = cfg.GetValue<int?>("RateLimiting:Sliding:WindowSeconds") ?? 60;
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

        if (!_store.TryAcquire(key, _limit, _window, out var retryAfterSeconds))
        {
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

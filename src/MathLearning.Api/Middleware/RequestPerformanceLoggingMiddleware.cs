using MathLearning.Infrastructure.Services.Performance;

namespace MathLearning.Api.Middleware;

public sealed class RequestPerformanceLoggingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<RequestPerformanceLoggingMiddleware> logger;

    public RequestPerformanceLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestPerformanceLoggingMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = DateTime.UtcNow;
        try
        {
            await next(context);
        }
        finally
        {
            var elapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;
            var queryCount = 0;
            if (context.Items.TryGetValue(PerformanceDbCommandInterceptor.QueryCountItemKey, out var value) && value is int count)
            {
                queryCount = count;
            }

            logger.LogInformation(
                "Request performance. Method={Method} Path={Path} StatusCode={StatusCode} ElapsedMs={ElapsedMs} DbQueryCount={DbQueryCount}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                Math.Round(elapsedMs, 2),
                queryCount);
        }
    }
}

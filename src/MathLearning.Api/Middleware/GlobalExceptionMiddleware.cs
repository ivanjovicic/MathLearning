using System.Diagnostics;

namespace MathLearning.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");

            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Response already started; cannot write problem response.");
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var correlationId =
                context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
                    ? value?.ToString()
                    : null;

            var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

            var problem = new
            {
                type = "https://httpstatuses.com/500",
                title = "Internal Server Error",
                status = 500,
                traceId,
                correlationId,
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}


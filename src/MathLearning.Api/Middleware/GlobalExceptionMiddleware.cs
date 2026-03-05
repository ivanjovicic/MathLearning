using System.Diagnostics;
using MathLearning.Application.DTOs.Common;

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
            context.Response.ContentType = "application/json";

            var correlationId =
                context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
                    ? value?.ToString()
                    : null;

            var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
            var retryAfter = ResolveRetryAfterSeconds(ex);
            var isRateLimited = ex is MathLearning.Api.Services.RateLimitedOperationException || retryAfter.HasValue;
            context.Response.StatusCode = isRateLimited
                ? StatusCodes.Status429TooManyRequests
                : StatusCodes.Status500InternalServerError;

            if (isRateLimited && retryAfter is > 0)
                context.Response.Headers.RetryAfter = retryAfter.Value.ToString();

            var problem = isRateLimited
                ? ApiResult<object>.RateLimited(
                    error: "Too many requests.",
                    errorDetails: new
                    {
                        correlationId,
                        exceptionType = ex.GetType().Name,
                        ex.Message
                    },
                    traceId: traceId,
                    retryAfterSeconds: retryAfter)
                : ApiResult<object>.Fail(
                    error: "Internal server error.",
                    errorCode: "INTERNAL_ERROR",
                    errorDetails: new
                    {
                        correlationId,
                        exceptionType = ex.GetType().Name,
                        ex.Message
                    },
                    traceId: traceId);

            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static int? ResolveRetryAfterSeconds(Exception ex)
    {
        if (ex is MathLearning.Api.Services.RateLimitedOperationException rateLimited)
            return rateLimited.RetryAfterSeconds;

        if (ex.Data.Contains("Retry-After"))
            return RetryAfterParser.ParseRetryAfterSeconds(ex.Data["Retry-After"]?.ToString());

        if (ex.Data.Contains("retry-after"))
            return RetryAfterParser.ParseRetryAfterSeconds(ex.Data["retry-after"]?.ToString());

        return null;
    }
}

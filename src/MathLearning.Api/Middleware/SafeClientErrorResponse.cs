using System.Diagnostics;

namespace MathLearning.Api.Middleware;

public static class SafeClientErrorResponse
{
    public const string GenericUnexpectedError = "An unexpected error occurred.";
    public const string GenericInternalError = "Internal server error.";

    public static object BuildClientDetails(HttpContext? context, string? traceId = null) =>
        new
        {
            correlationId = ResolveCorrelationId(context),
            traceId = traceId ?? ResolveTraceId(context),
        };

    public static IResult AuthUnexpectedFailure(
        HttpContext context,
        ILogger logger,
        Exception exception,
        string logMessage,
        params object[] args)
    {
        logger.LogError(exception, logMessage, args);
        return Results.Json(
            new
            {
                error = GenericUnexpectedError,
                traceId = ResolveTraceId(context),
                correlationId = ResolveCorrelationId(context),
            },
            statusCode: StatusCodes.Status500InternalServerError);
    }

    public static string? ResolveCorrelationId(HttpContext? context) =>
        context?.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value) == true
            ? value?.ToString()
            : null;

    public static string? ResolveTraceId(HttpContext? context) =>
        Activity.Current?.TraceId.ToString() ?? context?.TraceIdentifier;
}

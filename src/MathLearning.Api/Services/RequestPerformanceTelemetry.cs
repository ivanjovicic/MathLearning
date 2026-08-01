using System.Text.RegularExpressions;
using MathLearning.Infrastructure.Services.Performance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MathLearning.Api.Services;

public sealed record RequestPerformanceDecision(
    string RouteTemplate,
    int DbQueryCount,
    double ElapsedMs,
    string? Reason)
{
    public bool ShouldEmit => Reason is not null;
}

public static partial class RequestPerformanceTelemetry
{
    public const string RouteTemplateItemKey = "request-performance:route-template";
    public const string ReasonItemKey = "request-performance:reason";

    public static RequestPerformanceDecision Classify(
        HttpContext context,
        double elapsedMs,
        Exception? exception,
        double slowRequestThresholdMs,
        int queryBudget,
        double sampleRate)
    {
        var routeTemplate = ResolveRouteTemplate(context);
        var queryCount = ResolveDbQueryCount(context);
        var reason = DetermineReason(context, elapsedMs, queryCount, exception, slowRequestThresholdMs, queryBudget, sampleRate);
        return new RequestPerformanceDecision(routeTemplate, queryCount, elapsedMs, reason);
    }

    public static string ResolveRouteTemplate(HttpContext context)
    {
        if (context.GetEndpoint() is RouteEndpoint routeEndpoint &&
            !string.IsNullOrWhiteSpace(routeEndpoint.RoutePattern.RawText))
        {
            return routeEndpoint.RoutePattern.RawText;
        }

        var path = context.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(path))
            return "unknown";

        return NormalizePath(path);
    }

    public static int ResolveDbQueryCount(HttpContext context)
    {
        if (context.Items.TryGetValue(PerformanceDbCommandInterceptor.QueryCountItemKey, out var value) &&
            value is int count)
        {
            return count;
        }

        return 0;
    }

    public static bool IsLoggableRequest(HttpContext context)
    {
        var routeTemplate = ResolveRouteTemplate(context);
        return !IsIgnoredRoute(routeTemplate);
    }

    public static void CacheDecision(HttpContext context, RequestPerformanceDecision decision)
    {
        context.Items[RouteTemplateItemKey] = decision.RouteTemplate;
        context.Items[ReasonItemKey] = decision.Reason ?? string.Empty;
    }

    public static string? ReadCachedRouteTemplate(HttpContext context)
    {
        if (context.Items.TryGetValue(RouteTemplateItemKey, out var value) && value is string routeTemplate && !string.IsNullOrWhiteSpace(routeTemplate))
            return routeTemplate;

        return null;
    }

    public static string? ReadCachedReason(HttpContext context)
    {
        if (context.Items.TryGetValue(ReasonItemKey, out var value) && value is string reason && !string.IsNullOrWhiteSpace(reason))
            return reason;

        return null;
    }

    private static string? DetermineReason(
        HttpContext context,
        double elapsedMs,
        int queryCount,
        Exception? exception,
        double slowRequestThresholdMs,
        int queryBudget,
        double sampleRate)
    {
        if (!IsLoggableRequest(context))
            return null;

        if (exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
            return "error";

        if (elapsedMs >= slowRequestThresholdMs)
            return "slow_request";

        if (queryCount > queryBudget)
            return "query_budget";

        if (sampleRate <= 0)
            return null;

        if (sampleRate >= 1)
            return "sampled";

        var sampleHit = Random.Shared.NextDouble() < sampleRate;
        return sampleHit ? "sampled" : null;
    }

    private static bool IsIgnoredRoute(string routeTemplate)
    {
        return routeTemplate.Equals("/metrics", StringComparison.OrdinalIgnoreCase) ||
               routeTemplate.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
               routeTemplate.Equals("/health/ready", StringComparison.OrdinalIgnoreCase) ||
               routeTemplate.Equals("/health/schema", StringComparison.OrdinalIgnoreCase) ||
               routeTemplate.Equals("/health/background-jobs", StringComparison.OrdinalIgnoreCase) ||
               routeTemplate.Equals("/api/health/background-jobs", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return "/" + string.Join(
            "/",
            segments.Select(NormalizeSegment));
    }

    private static string NormalizeSegment(string segment)
    {
        if (Guid.TryParse(segment, out _))
            return "{guid}";

        if (long.TryParse(segment, out _))
            return "{id}";

        if (GuidSegmentRegex().IsMatch(segment))
            return "{id}";

        return segment;
    }

    [GeneratedRegex("^[A-Fa-f0-9]{12,}$", RegexOptions.CultureInvariant)]
    private static partial Regex GuidSegmentRegex();
}

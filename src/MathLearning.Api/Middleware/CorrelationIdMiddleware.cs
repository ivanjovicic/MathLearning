using Serilog.Context;

namespace MathLearning.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";
    public const int MaxLength = 128;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers);
        context.Items[ItemKey] = correlationId;

        // Echo back so clients can correlate requests and logs.
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(IHeaderDictionary headers)
    {
        if (!headers.TryGetValue(HeaderName, out var values) || values.Count != 1)
            return Guid.NewGuid().ToString();

        var candidate = values[0]?.Trim();
        if (string.IsNullOrEmpty(candidate) ||
            candidate.Length > MaxLength ||
            candidate.Any(character => !IsSafeCharacter(character)))
        {
            return Guid.NewGuid().ToString();
        }

        return candidate;
    }

    private static bool IsSafeCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-';
}

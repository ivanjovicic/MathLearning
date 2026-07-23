using System.Text.Json;
using MathLearning.Api.Middleware;
using MathLearning.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Middleware;

public sealed class GlobalExceptionMiddlewareTests
{
    private const string SecretMessage = "SECRET_DATABASE_PASSWORD=postgres-auth-failed";

    [Fact]
    public async Task Invoke_WhenUnhandledException_ReturnsSafeErrorWithoutRawMessage()
    {
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException(SecretMessage));
        var context = CreateHttpContext();

        await middleware.Invoke(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var body = await ReadBodyAsync(context);
        Assert.DoesNotContain(SecretMessage, body);
        Assert.DoesNotContain("InvalidOperationException", body);

        using var json = JsonDocument.Parse(body);
        Assert.Equal("INTERNAL_ERROR", json.RootElement.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
        var details = json.RootElement.GetProperty("errorDetails");
        Assert.True(details.TryGetProperty("traceId", out _));
        Assert.Equal("corr-test-001", details.GetProperty("correlationId").GetString());
        Assert.False(details.TryGetProperty("exceptionType", out _));
    }

    [Fact]
    public async Task Invoke_WhenUnhandledException_LogsCorrelationAndTraceAsStructuredProperties()
    {
        var logger = new CollectingLogger<GlobalExceptionMiddleware>();
        var middleware = CreateMiddleware(
            _ => throw new InvalidOperationException(SecretMessage),
            logger);
        var context = CreateHttpContext();

        await middleware.Invoke(context);

        var entry = Assert.Single(logger.Entries, item => item.Level == LogLevel.Error);
        Assert.Equal("corr-test-001", entry.Properties["CorrelationId"]?.ToString());
        Assert.Equal("trace-test-001", entry.Properties["TraceId"]?.ToString());
        Assert.DoesNotContain(SecretMessage, entry.Message);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task Invoke_WhenRateLimited_ReturnsRetryAfterWithoutRawMessage()
    {
        var middleware = CreateMiddleware(_ => throw new RateLimitedOperationException(SecretMessage, retryAfterSeconds: 12));
        var context = CreateHttpContext();

        await middleware.Invoke(context);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("12", context.Response.Headers.RetryAfter.ToString());

        var body = await ReadBodyAsync(context);
        Assert.DoesNotContain(SecretMessage, body);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("isRateLimited").GetBoolean());
        Assert.Equal(12, json.RootElement.GetProperty("retryAfterSeconds").GetInt32());
    }

    private static GlobalExceptionMiddleware CreateMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware>? logger = null) =>
        new(next, logger ?? NullLogger<GlobalExceptionMiddleware>.Instance);

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.ItemKey] = "corr-test-001";
        context.TraceIdentifier = "trace-test-001";
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception, properties));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

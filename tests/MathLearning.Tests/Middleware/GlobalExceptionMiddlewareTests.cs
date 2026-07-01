using System.Text.Json;
using MathLearning.Api.Middleware;
using MathLearning.Api.Services;
using MathLearning.Application.DTOs.Common;
using Microsoft.AspNetCore.Http;
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
        Assert.False(details.TryGetProperty("exceptionType", out _));
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

    private static GlobalExceptionMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, NullLogger<GlobalExceptionMiddleware>.Instance);

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
}

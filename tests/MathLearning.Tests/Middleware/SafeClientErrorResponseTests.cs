using System.Diagnostics;
using System.Text.Json;
using MathLearning.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace MathLearning.Tests.Middleware;

public sealed class SafeClientErrorResponseTests
{
    [Fact]
    public void ResolveCorrelationId_WithNullOrMissingContext_ReturnsNull()
    {
        Assert.Null(SafeClientErrorResponse.ResolveCorrelationId(null));
        Assert.Null(SafeClientErrorResponse.ResolveCorrelationId(new DefaultHttpContext()));
    }

    [Fact]
    public void ResolveCorrelationId_UsesStoredValueToString()
    {
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.ItemKey] = 12345;

        var correlationId = SafeClientErrorResponse.ResolveCorrelationId(context);

        Assert.Equal("12345", correlationId);
    }

    [Fact]
    public void ResolveTraceId_WithoutActivity_UsesHttpTraceIdentifier()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "http-trace-1"
        };

        var traceId = SafeClientErrorResponse.ResolveTraceId(context);

        Assert.Equal("http-trace-1", traceId);
    }

    [Fact]
    public void ResolveTraceId_WithCurrentActivity_PrefersActivityTraceId()
    {
        using var activity = new Activity("safe-error-test").SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "http-fallback"
        };

        var traceId = SafeClientErrorResponse.ResolveTraceId(context);

        Assert.Equal(activity.TraceId.ToString(), traceId);
    }

    [Fact]
    public void BuildClientDetails_UsesProvidedTraceAndStoredCorrelationId()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "http-trace"
        };
        context.Items[CorrelationIdMiddleware.ItemKey] = "correlation-7";

        var details = SafeClientErrorResponse.BuildClientDetails(context, traceId: "provided-trace");
        var json = JsonSerializer.SerializeToElement(details);

        Assert.Equal("correlation-7", json.GetProperty("correlationId").GetString());
        Assert.Equal("provided-trace", json.GetProperty("traceId").GetString());
    }

    [Fact]
    public void BuildClientDetails_WithoutExplicitTrace_UsesContextTraceIdentifier()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "context-trace"
        };

        var details = SafeClientErrorResponse.BuildClientDetails(context);
        var json = JsonSerializer.SerializeToElement(details);

        Assert.Equal(JsonValueKind.Null, json.GetProperty("correlationId").ValueKind);
        Assert.Equal("context-trace", json.GetProperty("traceId").GetString());
    }

    [Fact]
    public void AuthUnexpectedFailure_LogsExceptionAndReturnsResult()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-500"
        };
        context.Items[CorrelationIdMiddleware.ItemKey] = "corr-500";
        var logger = new Mock<ILogger>();
        var exception = new InvalidOperationException("sensitive failure");

        var result = SafeClientErrorResponse.AuthUnexpectedFailure(
            context,
            logger.Object,
            exception,
            "Operation {OperationId} failed",
            42);

        Assert.NotNull(result);
        Assert.Single(logger.Invocations);
        Assert.Equal(SafeClientErrorResponse.GenericUnexpectedError, "An unexpected error occurred.");
        Assert.Equal(SafeClientErrorResponse.GenericInternalError, "Internal server error.");
    }
}

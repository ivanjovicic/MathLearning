using MathLearning.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace MathLearning.Tests.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Invoke_WithIncomingCorrelationId_PreservesAndEchoesValue()
    {
        var nextCalled = false;
        var middleware = new CorrelationIdMiddleware(context =>
        {
            nextCalled = true;
            Assert.Equal("incoming-correlation", context.Items[CorrelationIdMiddleware.ItemKey]);
            Assert.Equal(
                "incoming-correlation",
                context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "incoming-correlation";

        await middleware.Invoke(context);

        Assert.True(nextCalled);
        Assert.Equal("incoming-correlation", context.Items[CorrelationIdMiddleware.ItemKey]);
        Assert.Equal(
            "incoming-correlation",
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task Invoke_WithMissingHeader_GeneratesGuidCorrelationId()
    {
        string? observedCorrelationId = null;
        var middleware = new CorrelationIdMiddleware(context =>
        {
            observedCorrelationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString();
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();

        await middleware.Invoke(context);

        Assert.NotNull(observedCorrelationId);
        Assert.True(Guid.TryParse(observedCorrelationId, out _));
        Assert.Equal(
            observedCorrelationId,
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task Invoke_WithWhitespaceHeader_GeneratesNewGuidInsteadOfEchoingWhitespace()
    {
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "   ";

        await middleware.Invoke(context);

        AssertGeneratedGuid(context, rejectedValue: "   ");
    }

    [Theory]
    [InlineData("child@example.com")]
    [InlineData("unsafe value with spaces")]
    [InlineData("value/with/slashes")]
    public async Task Invoke_WithUnsafeHeader_GeneratesSafeReplacement(string unsafeValue)
    {
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = unsafeValue;

        await middleware.Invoke(context);

        AssertGeneratedGuid(context, rejectedValue: unsafeValue);
    }

    [Fact]
    public async Task Invoke_WithOversizedHeader_GeneratesSafeReplacement()
    {
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        var oversized = new string('a', CorrelationIdMiddleware.MaxLength + 1);
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = oversized;

        await middleware.Invoke(context);

        AssertGeneratedGuid(context, rejectedValue: oversized);
    }

    [Fact]
    public async Task Invoke_WithMultipleHeaderValues_GeneratesSafeReplacement()
    {
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] =
            new StringValues(["first-correlation", "second-correlation"]);

        await middleware.Invoke(context);

        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString();
        Assert.True(Guid.TryParse(correlationId, out _));
        Assert.Equal(
            correlationId,
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    private static void AssertGeneratedGuid(
        DefaultHttpContext context,
        string rejectedValue)
    {
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString();
        Assert.NotNull(correlationId);
        Assert.True(Guid.TryParse(correlationId, out _));
        Assert.NotEqual(rejectedValue, correlationId);
        Assert.Equal(
            correlationId,
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }
}

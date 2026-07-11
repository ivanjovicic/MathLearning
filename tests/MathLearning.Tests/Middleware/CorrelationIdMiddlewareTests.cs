using MathLearning.Api.Middleware;
using Microsoft.AspNetCore.Http;

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

        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString();
        Assert.NotNull(correlationId);
        Assert.True(Guid.TryParse(correlationId, out _));
        Assert.NotEqual("   ", correlationId);
        Assert.Equal(
            correlationId,
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }
}

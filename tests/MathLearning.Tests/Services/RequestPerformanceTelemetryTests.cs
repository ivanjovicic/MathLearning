using MathLearning.Api.Services;
using MathLearning.Infrastructure.Services.Performance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace MathLearning.Tests.Services;

public sealed class RequestPerformanceTelemetryTests
{
    [Fact]
    public void ResolveRouteTemplate_UsesRoutePatternInsteadOfRawPath()
    {
        var context = CreateContext(
            routePattern: "/api/cosmetics/items/{itemKey}/claim",
            rawPath: "/api/cosmetics/items/frame_comet/claim",
            queryCount: 0);

        var template = RequestPerformanceTelemetry.ResolveRouteTemplate(context);

        Assert.Equal("/api/cosmetics/items/{itemKey}/claim", template);
    }

    [Fact]
    public void Classify_ReturnsWarningOnlyForSlowBudgetOrSampledRequests()
    {
        var context = CreateContext("/api/quiz/{quizId}/answer", "/api/quiz/123/answer", queryCount: 5);

        var fast = RequestPerformanceTelemetry.Classify(
            context,
            elapsedMs: 49,
            exception: null,
            slowRequestThresholdMs: 50,
            queryBudget: 10,
            sampleRate: 0);

        var slow = RequestPerformanceTelemetry.Classify(
            context,
            elapsedMs: 50,
            exception: null,
            slowRequestThresholdMs: 50,
            queryBudget: 10,
            sampleRate: 0);

        var overBudget = RequestPerformanceTelemetry.Classify(
            context,
            elapsedMs: 10,
            exception: null,
            slowRequestThresholdMs: 50,
            queryBudget: 4,
            sampleRate: 0);

        Assert.False(fast.ShouldEmit);
        Assert.Null(fast.Reason);
        Assert.True(slow.ShouldEmit);
        Assert.Equal("slow_request", slow.Reason);
        Assert.True(overBudget.ShouldEmit);
        Assert.Equal("query_budget", overBudget.Reason);
    }

    private static DefaultHttpContext CreateContext(string routePattern, string rawPath, int queryCount)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = rawPath;
        context.Items[PerformanceDbCommandInterceptor.QueryCountItemKey] = queryCount;
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(routePattern),
            order: 0,
            metadata: new EndpointMetadataCollection(),
            displayName: routePattern));
        return context;
    }
}

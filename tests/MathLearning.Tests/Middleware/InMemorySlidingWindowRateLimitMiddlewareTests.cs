using System.Net;
using System.Text.Json;
using MathLearning.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace MathLearning.Tests.Middleware;

public sealed class InMemorySlidingWindowRateLimitMiddlewareTests
{
    [Fact]
    public async Task Invoke_SpoofedForwardedFor_DoesNotCreateSeparateBuckets()
    {
        var store = new InMemoryRateLimitCounterStore();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Sliding:Limit"] = "2",
                ["RateLimiting:Sliding:WindowSeconds"] = "60"
            })
            .Build();

        var middleware = new InMemorySlidingWindowRateLimitMiddleware(
            _ => Task.CompletedTask,
            configuration,
            store);

        for (var i = 0; i < 2; i++)
        {
            var context = CreateContext(spoofedRemoteIp: $"1.0.0.{i + 1}", physicalIp: "198.51.100.4");
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        var limited = CreateContext(spoofedRemoteIp: "8.8.8.8", physicalIp: "198.51.100.4");
        await middleware.Invoke(limited);
        Assert.Equal(StatusCodes.Status429TooManyRequests, limited.Response.StatusCode);

        var body = await ReadBodyAsync(limited);
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("isRateLimited").GetBoolean());
    }

    [Fact]
    public async Task Invoke_AuthenticatedUser_RateLimitsByUserNotForwardedIp()
    {
        var store = new InMemoryRateLimitCounterStore();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Sliding:Limit"] = "1",
                ["RateLimiting:Sliding:WindowSeconds"] = "60"
            })
            .Build();

        var middleware = new InMemorySlidingWindowRateLimitMiddleware(
            _ => Task.CompletedTask,
            configuration,
            store);

        var first = CreateContext(spoofedRemoteIp: "1.1.1.1", physicalIp: "198.51.100.4", userId: "same-user");
        await middleware.Invoke(first);
        Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);

        var second = CreateContext(spoofedRemoteIp: "2.2.2.2", physicalIp: "198.51.100.5", userId: "same-user");
        await middleware.Invoke(second);
        Assert.Equal(StatusCodes.Status429TooManyRequests, second.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_HealthEndpoint_IsExempt()
    {
        var store = new InMemoryRateLimitCounterStore();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Sliding:Limit"] = "1",
                ["RateLimiting:Sliding:WindowSeconds"] = "60"
            })
            .Build();

        var middleware = new InMemorySlidingWindowRateLimitMiddleware(
            _ => Task.CompletedTask,
            configuration,
            store);

        for (var i = 0; i < 3; i++)
        {
            var context = CreateContext(spoofedRemoteIp: "1.1.1.1", physicalIp: "198.51.100.4");
            context.Request.Path = "/health";
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
    }

    private static DefaultHttpContext CreateContext(string spoofedRemoteIp, string physicalIp, string? userId = null)
    {
        var context = new DefaultHttpContext
        {
            Connection = { RemoteIpAddress = IPAddress.Parse(spoofedRemoteIp) },
            Request = { Path = "/api/auth/test" },
            Response = { Body = new MemoryStream() }
        };
        context.Items[ConnectionRemoteIpMiddleware.ItemKey] = IPAddress.Parse(physicalIp);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            context.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    [new System.Security.Claims.Claim("userId", userId)],
                    authenticationType: "Test"));
        }

        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}

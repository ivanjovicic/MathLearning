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

    [Fact]
    public async Task Invoke_ReturnsActualRetryAfterHeaderFromStore()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 7, 28, 0, 0, 0, TimeSpan.Zero));
        var store = new InMemoryRateLimitCounterStore(time);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Sliding:Limit"] = "1",
                ["RateLimiting:Sliding:WindowSeconds"] = "10"
            })
            .Build();

        var middleware = new InMemorySlidingWindowRateLimitMiddleware(
            _ => Task.CompletedTask,
            configuration,
            store);

        var first = CreateContext(spoofedRemoteIp: "1.1.1.1", physicalIp: "198.51.100.4");
        await middleware.Invoke(first);
        Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);

        time.Advance(TimeSpan.FromSeconds(3));

        var limited = CreateContext(spoofedRemoteIp: "1.1.1.1", physicalIp: "198.51.100.4");
        await middleware.Invoke(limited);

        Assert.Equal(StatusCodes.Status429TooManyRequests, limited.Response.StatusCode);
        Assert.Equal("7", limited.Response.Headers["Retry-After"].ToString());

        var body = await ReadBodyAsync(limited);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(7, json.RootElement.GetProperty("retryAfterSeconds").GetInt32());
    }

    [Fact]
    public async Task Invoke_LocalReplicasUseIndependentStores()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Sliding:Limit"] = "1",
                ["RateLimiting:Sliding:WindowSeconds"] = "60"
            })
            .Build();

        var storeOne = new InMemoryRateLimitCounterStore();
        var storeTwo = new InMemoryRateLimitCounterStore();

        var replicaOne = new InMemorySlidingWindowRateLimitMiddleware(
            _ => Task.CompletedTask,
            configuration,
            storeOne);

        var replicaTwo = new InMemorySlidingWindowRateLimitMiddleware(
            _ => Task.CompletedTask,
            configuration,
            storeTwo);

        var firstReplicaOne = CreateContext(spoofedRemoteIp: "1.1.1.1", physicalIp: "198.51.100.4", userId: "shared-user");
        await replicaOne.Invoke(firstReplicaOne);
        Assert.Equal(StatusCodes.Status200OK, firstReplicaOne.Response.StatusCode);

        var firstReplicaTwo = CreateContext(spoofedRemoteIp: "2.2.2.2", physicalIp: "198.51.100.5", userId: "shared-user");
        await replicaTwo.Invoke(firstReplicaTwo);
        Assert.Equal(StatusCodes.Status200OK, firstReplicaTwo.Response.StatusCode);

        var secondReplicaOne = CreateContext(spoofedRemoteIp: "3.3.3.3", physicalIp: "198.51.100.6", userId: "shared-user");
        await replicaOne.Invoke(secondReplicaOne);
        Assert.Equal(StatusCodes.Status429TooManyRequests, secondReplicaOne.Response.StatusCode);

        var secondReplicaTwo = CreateContext(spoofedRemoteIp: "4.4.4.4", physicalIp: "198.51.100.7", userId: "shared-user");
        await replicaTwo.Invoke(secondReplicaTwo);
        Assert.Equal(StatusCodes.Status429TooManyRequests, secondReplicaTwo.Response.StatusCode);

        Assert.Equal(1, storeOne.GetSnapshot().AllowedRequests);
        Assert.Equal(1, storeTwo.GetSnapshot().AllowedRequests);
    }

    [Theory]
    [InlineData("0", "60", "100")]
    [InlineData("1", "0", "100")]
    [InlineData("2", "60", "1")]
    public void Constructor_InvalidConfiguration_Throws(string limit, string windowSeconds, string maxPartitions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Sliding:Limit"] = limit,
                ["RateLimiting:Sliding:WindowSeconds"] = windowSeconds,
                ["RateLimiting:Sliding:MaxPartitions"] = maxPartitions
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new InMemorySlidingWindowRateLimitMiddleware(
            _ => Task.CompletedTask,
            configuration,
            new InMemoryRateLimitCounterStore()));
    }

    [Fact]
    public async Task Invoke_CanceledRequests_DoNotLeakPartitions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Sliding:Limit"] = "1000",
                ["RateLimiting:Sliding:WindowSeconds"] = "60",
                ["RateLimiting:Sliding:MaxPartitions"] = "1000"
            })
            .Build();

        var store = new InMemoryRateLimitCounterStore();
        var middleware = new InMemorySlidingWindowRateLimitMiddleware(
            context => Task.FromCanceled(context.RequestAborted),
            configuration,
            store);

        for (var i = 0; i < 1000; i++)
        {
            var context = CreateContext(spoofedRemoteIp: "5.5.5.5", physicalIp: "198.51.100.8", userId: "cancel-user");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            context.RequestAborted = cancellation.Token;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => middleware.Invoke(context));
        }

        var snapshot = store.GetSnapshot();
        Assert.Equal(1, snapshot.PartitionCount);
        Assert.Equal(1000, snapshot.AllowedRequests);
        Assert.Equal(0, snapshot.RejectedRequests);
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

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}

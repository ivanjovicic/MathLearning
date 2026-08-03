using System.Net;
using System.Text.Json;
using MathLearning.Application.Services;
using MathLearning.Tests.Middleware;

namespace MathLearning.Tests.Endpoints;

public sealed class RateLimitMetricsEndpointTests : IClassFixture<RateLimitTestWebApplicationFactory>
{
    private readonly HttpClient client;

    public RateLimitMetricsEndpointTests(RateLimitTestWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task MetricsExposeRateLimitSnapshotAfterRequests()
    {
        for (var i = 0; i < 2; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/test");
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", $"203.0.113.{i + 1}");

            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var limited = new HttpRequestMessage(HttpMethod.Get, "/auth/test");
        limited.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.99");

        var limitedResponse = await client.SendAsync(limited);
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);

        var metricsResponse = await client.GetAsync("/metrics");
        Assert.True(
            metricsResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Anonymous /metrics expected 401/403, got {(int)metricsResponse.StatusCode}");

        using var adminRequest = new HttpRequestMessage(HttpMethod.Get, "/metrics");
        adminRequest.Headers.Add("X-Test-UserId", "admin-user");
        adminRequest.Headers.Add("X-Test-Roles", DesignTokenSecurity.AdminRole);

        var adminMetricsResponse = await client.SendAsync(adminRequest);
        Assert.Equal(HttpStatusCode.OK, adminMetricsResponse.StatusCode);

        var body = await adminMetricsResponse.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var rateLimit = json.RootElement.GetProperty("rateLimit");
        var explanationCache = json.RootElement.GetProperty("explanationCache");

        Assert.Equal(1, rateLimit.GetProperty("partitionCount").GetInt32());
        Assert.Equal(2, rateLimit.GetProperty("allowedRequests").GetInt64());
        Assert.Equal(1, rateLimit.GetProperty("rejectedRequests").GetInt64());
        Assert.Equal(0, rateLimit.GetProperty("saturationRejections").GetInt64());
        Assert.True(rateLimit.GetProperty("cleanupRuns").GetInt64() >= 0);
        Assert.Equal(0, explanationCache.GetProperty("hitCount").GetInt64());
        Assert.Equal(0, explanationCache.GetProperty("missCount").GetInt64());
    }
}

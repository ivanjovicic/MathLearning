using System.Net;
using MathLearning.Api;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MathLearning.Tests.Middleware;

public sealed class ForwardedHeadersProxyTrustIntegrationTests : IClassFixture<RateLimitTestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ForwardedHeadersProxyTrustIntegrationTests(RateLimitTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SpoofedXForwardedFor_CannotBypassSlidingWindowRateLimit()
    {
        for (var i = 0; i < 2; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/test");
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", $"203.0.113.{i + 1}");

            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var limited = new HttpRequestMessage(HttpMethod.Get, "/auth/test");
        limited.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.99");

        var limitedResponse = await _client.SendAsync(limited);
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        Assert.True(limitedResponse.Headers.Contains("Retry-After"));
    }
}

public sealed class RateLimitTestWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Sliding:Limit"] = "2",
                ["RateLimiting:Sliding:WindowSeconds"] = "60"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<MathLearning.Api.Middleware.IRateLimitCounterStore>();
            services.AddSingleton<MathLearning.Api.Middleware.IRateLimitCounterStore, MathLearning.Api.Middleware.InMemoryRateLimitCounterStore>();
        });
    }
}

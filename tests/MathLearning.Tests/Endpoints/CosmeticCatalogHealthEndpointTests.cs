using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Tests.Helpers;

namespace MathLearning.Tests.Endpoints;

public sealed class CosmeticCatalogHealthEndpointTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public CosmeticCatalogHealthEndpointTests(CustomWebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Ready_ReturnsNotReady_WhenCatalogRevisionIsMissing()
    {
        using var response = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual("Ready", payload.GetProperty("status").GetString());
        Assert.Contains(
            payload.GetProperty("reason").GetString(),
            new[] { "SchemaNotReady", "CosmeticCatalogRevisionMissing", "CosmeticCatalogDefaultsMissing", "CosmeticCatalogFragmentsInvalid", "CosmeticCatalogRewardsInvalid" });
    }
}

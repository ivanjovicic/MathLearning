using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Tests.Helpers;

namespace MathLearning.Tests.Endpoints;

public sealed class HealthEndpointContractTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointContractTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/health/schema")]
    [InlineData("/api/health/schema")]
    public async Task SchemaHealthRoutes_ReturnHealthStatus_AndExpectedShape(string path)
    {
        var response = await _client.GetAsync(path);

        AssertHealthStatus(response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("status", out var statusElement));
        var status = statusElement.GetString();
        Assert.False(string.Equals(status, "Healthy", StringComparison.Ordinal));
        Assert.False(string.Equals(status, "Unhealthy", StringComparison.Ordinal));
        Assert.True(payload.TryGetProperty("isSchemaReady", out _));
        Assert.True(payload.TryGetProperty("latestCodeMigration", out _));
        Assert.True(payload.TryGetProperty("latestAppliedMigration", out _));
        Assert.True(payload.TryGetProperty("pendingMigrationsCount", out _));
        Assert.True(payload.TryGetProperty("unknownAppliedMigrationsCount", out _));
        Assert.True(payload.TryGetProperty("failureMessage", out _));
        Assert.True(payload.TryGetProperty("checkedAtUtc", out _));
    }

    [Theory]
    [InlineData("/api/health/db")]
    [InlineData("/api/health/ready")]
    public async Task ExistingHealthRoutes_ReturnHealthStatus(string path)
    {
        var response = await _client.GetAsync(path);

        AssertHealthStatus(response.StatusCode);
    }

    private static void AssertHealthStatus(HttpStatusCode statusCode)
    {
        Assert.True(
            statusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Expected 200 or 503, got {(int)statusCode} {statusCode}.");
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Application.Services;
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
    [InlineData("/api/health/")]
    [InlineData("/health")]
    public async Task PublicLivenessRoutes_RemainAnonymous_AndExposeStatus(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        if (path.StartsWith("/api/health", StringComparison.Ordinal))
        {
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Healthy", payload.GetProperty("status").GetString());
            Assert.True(payload.TryGetProperty("timestamp", out _));
            Assert.False(payload.TryGetProperty("schema", out _));
            Assert.False(payload.TryGetProperty("data", out _));
            Assert.False(payload.TryGetProperty("failureMessage", out _));
        }
    }

    [Theory]
    [InlineData("/api/health/db")]
    [InlineData("/api/health/ready")]
    public async Task PublicReadyAndDbRoutes_ExposeOnlySafeStatusFields(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await _client.SendAsync(request);
        AssertHealthStatus(response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("status", out _));
        Assert.True(payload.TryGetProperty("timestamp", out _));
        Assert.False(payload.TryGetProperty("schema", out _));
        Assert.False(payload.TryGetProperty("data", out _));
        Assert.False(payload.TryGetProperty("catalog", out _));
        Assert.False(payload.TryGetProperty("provider", out _));
        Assert.False(payload.TryGetProperty("latestCodeMigration", out _));
        Assert.False(payload.TryGetProperty("latestAppliedMigration", out _));
        Assert.False(payload.TryGetProperty("failureMessage", out _));
        Assert.False(payload.TryGetProperty("threadCount", out _));

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            Assert.True(payload.TryGetProperty("reason", out var reasonElement));
            var reason = reasonElement.GetString();
            Assert.False(string.IsNullOrWhiteSpace(reason));
            Assert.DoesNotContain("migration", reason!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("exception", reason!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("/health/schema")]
    [InlineData("/api/health/schema")]
    public async Task SchemaHealthRoutes_DenyAnonymous(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await _client.SendAsync(request);
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403 but got {(int)response.StatusCode}");
    }

    [Theory]
    [InlineData("/health/schema")]
    [InlineData("/api/health/schema")]
    public async Task SchemaHealthRoutes_DenyNonAdmin(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-UserId", "regular-user");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/health/schema")]
    [InlineData("/api/health/schema")]
    public async Task SchemaHealthRoutes_Admin_RetainsDiagnosticDetail(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-UserId", "admin-user");
        request.Headers.Add("X-Test-Roles", DesignTokenSecurity.AdminRole);

        var response = await _client.SendAsync(request);
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

    private static void AssertHealthStatus(HttpStatusCode statusCode)
    {
        Assert.True(
            statusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Expected 200 or 503, got {(int)statusCode} {statusCode}.");
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class MonitoringLogAuthorizationTests :
    IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string SecretEmail = "leak-test@internal.example";
    private const string SecretToken = "Bearer super-secret-jwt-token-value";

    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public MonitoringLogAuthorizationTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/monitoring/logs")]
    [InlineData("/api/monitoring/logs-advanced")]
    [InlineData("/api/logs/recent")]
    public async Task AnonymousUser_CannotReadLogEndpoints(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await client.SendAsync(request);
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403 but got {(int)response.StatusCode}");
    }

    [Theory]
    [InlineData("/api/monitoring/logs")]
    [InlineData("/api/monitoring/logs-advanced")]
    [InlineData("/api/logs/recent")]
    public async Task AuthenticatedNonAdmin_CannotReadLogEndpoints(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-UserId", "regular-user");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanReadMonitoringLogs_WithRedaction()
    {
        WriteMonitoringLogFile($"Login failed for {SecretEmail} with {SecretToken}");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/monitoring/logs");
        request.Headers.Add("X-Test-UserId", "admin-user");
        request.Headers.Add("X-Test-Roles", DesignTokenSecurity.AdminRole);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretEmail, body);
        Assert.DoesNotContain(SecretToken, body);
        Assert.Contains("[redacted-email]", body);
        Assert.Contains("[redacted-token]", body);
    }

    [Fact]
    public async Task Admin_CanReadDatabaseLogs_WithRedaction()
    {
        await SeedApplicationLogAsync(SecretEmail, SecretToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/logs/recent?limit=5");
        request.Headers.Add("X-Test-UserId", "admin-user");
        request.Headers.Add("X-Test-Roles", DesignTokenSecurity.AdminRole);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretEmail, body);
        Assert.DoesNotContain(SecretToken, body);
        Assert.Contains("[redacted-email]", body);
        Assert.Contains("[redacted-token]", body);
    }

    [Fact]
    public async Task Admin_CanReadDatabaseLogs_ButLimitIsClamped()
    {
        for (var i = 0; i < 150; i++)
        {
            await SeedApplicationLogAsync($"{SecretEmail}-{i}", $"{SecretToken}-{i}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/logs/recent?limit=999");
        request.Headers.Add("X-Test-UserId", "admin-user");
        request.Headers.Add("X-Test-Roles", DesignTokenSecurity.AdminRole);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(100, json.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Admin_CanReadMonitoringLogAdvanced_ButItStaysBounded()
    {
        WriteMonitoringLogFile(Enumerable.Range(1, 250)
            .Select(i => $"line {i}: {SecretEmail} {SecretToken}")
            .ToArray());

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/monitoring/logs-advanced?search=line");
        request.Headers.Add("X-Test-UserId", "admin-user");
        request.Headers.Add("X-Test-Roles", DesignTokenSecurity.AdminRole);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(200, json.RootElement.GetArrayLength());
        Assert.DoesNotContain(SecretEmail, body);
        Assert.DoesNotContain(SecretToken, body);
    }

    [Fact]
    public async Task HealthRemainsAnonymous_MetricsRequireAdmin()
    {
        using var healthRequest = new HttpRequestMessage(HttpMethod.Get, "/health");
        healthRequest.Headers.Add(TestAuthHandler.AnonymousHeader, "true");
        using var metricsRequest = new HttpRequestMessage(HttpMethod.Get, "/metrics");
        metricsRequest.Headers.Add(TestAuthHandler.AnonymousHeader, "true");
        using var jobsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/monitoring/jobs");
        jobsRequest.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(healthRequest)).StatusCode);

        var metricsStatus = (await client.SendAsync(metricsRequest)).StatusCode;
        Assert.True(
            metricsStatus is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Anonymous /metrics expected 401/403, got {(int)metricsStatus}");

        var jobsStatus = (await client.SendAsync(jobsRequest)).StatusCode;
        Assert.True(
            jobsStatus is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Anonymous /api/monitoring/jobs expected 401/403, got {(int)jobsStatus}");
    }

    [Fact]
    public async Task Admin_CanReadMetricsAndMonitoringJobs()
    {
        using var metricsRequest = new HttpRequestMessage(HttpMethod.Get, "/metrics");
        metricsRequest.Headers.Add("X-Test-UserId", "admin-user");
        metricsRequest.Headers.Add("X-Test-Roles", DesignTokenSecurity.AdminRole);

        using var jobsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/monitoring/jobs");
        jobsRequest.Headers.Add("X-Test-UserId", "admin-user");
        jobsRequest.Headers.Add("X-Test-Roles", DesignTokenSecurity.AdminRole);

        var metricsResponse = await client.SendAsync(metricsRequest);
        Assert.Equal(HttpStatusCode.OK, metricsResponse.StatusCode);
        using var metricsJson = JsonDocument.Parse(await metricsResponse.Content.ReadAsStringAsync());
        Assert.True(metricsJson.RootElement.TryGetProperty("rateLimit", out _));
        Assert.True(metricsJson.RootElement.TryGetProperty("threadCount", out _));

        var jobsResponse = await client.SendAsync(jobsRequest);
        Assert.Equal(HttpStatusCode.OK, jobsResponse.StatusCode);
        using var jobsJson = JsonDocument.Parse(await jobsResponse.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, jobsJson.RootElement.ValueKind);
        Assert.True(jobsJson.RootElement.GetArrayLength() >= 1);
    }

    private void WriteMonitoringLogFile(params string[] lines)
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDir);
        File.WriteAllLines(Path.Combine(logDir, "log.txt"), lines, Encoding.UTF8);
    }

    private async Task SeedApplicationLogAsync(string email, string token)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        db.ApplicationLogs.Add(new ApplicationLog
        {
            Timestamp = DateTime.UtcNow,
            Level = "Error",
            Message = $"Auth failure for {email}",
            Exception = $"Header {token}",
            RequestPath = "/auth/login",
            UserName = email,
            MachineName = Environment.MachineName
        });
        await db.SaveChangesAsync();
    }
}

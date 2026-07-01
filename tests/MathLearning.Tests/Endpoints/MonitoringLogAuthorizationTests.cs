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
        var response = await client.GetAsync(path);
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
    public async Task HealthAndMetrics_RemainAnonymous()
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/metrics")).StatusCode);
    }

    private void WriteMonitoringLogFile(string line)
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(Path.Combine(logDir, "log.txt"), line + Environment.NewLine, Encoding.UTF8);
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

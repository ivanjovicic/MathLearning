using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Bugs;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class BugScreenshotEndpointIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public BugScreenshotEndpointIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task ScreenshotRoute_UsesAuthorizedApiRoute_AndStreamsBytesForReporterAdminAndNoObject()
    {
        var report = await ReportBugWithScreenshotAsync("learner-1");
        Assert.Equal(HttpStatusCode.Created, report.StatusCode);

        var bug = await report.Content.ReadFromJsonAsync<BugReportDto>();
        Assert.NotNull(bug);
        Assert.False(string.IsNullOrWhiteSpace(bug!.ScreenshotUrl));
        Assert.StartsWith($"/api/bugs/{bug.Id}/screenshot", bug.ScreenshotUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("/uploads/screenshots", bug.ScreenshotUrl, StringComparison.OrdinalIgnoreCase);

        var reporter = await GetScreenshotAsync(bug.Id, "learner-1");
        Assert.Equal(HttpStatusCode.OK, reporter.StatusCode);
        Assert.Equal("image/png", reporter.Content.Headers.ContentType?.MediaType);
        Assert.Equal(MinimalPng, await reporter.Content.ReadAsByteArrayAsync());

        var admin = await GetScreenshotAsync(bug.Id, "admin-1", DesignTokenSecurity.AdminRole);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        Assert.Equal("image/png", admin.Content.Headers.ContentType?.MediaType);
        Assert.Equal(MinimalPng, await admin.Content.ReadAsByteArrayAsync());

        var crossUser = await GetScreenshotAsync(bug.Id, "learner-2");
        Assert.Equal(HttpStatusCode.Forbidden, crossUser.StatusCode);

        using var anonymousRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/bugs/{bug.Id}/screenshot");
        anonymousRequest.Headers.Add(TestAuthHandler.AnonymousHeader, "true");
        var anonymous = await client.SendAsync(anonymousRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var bugService = scope.ServiceProvider.GetRequiredService<IBugReportService>();
            var screenshotStorage = scope.ServiceProvider.GetRequiredService<IScreenshotStorageService>();
            var screenshotInfo = await bugService.GetBugReportScreenshotInfoAsync(bug.Id);

            Assert.NotNull(screenshotInfo);
            Assert.False(string.IsNullOrWhiteSpace(screenshotInfo!.ScreenshotStorageKey));

            Assert.True(await screenshotStorage.DeleteScreenshotAsync(screenshotInfo.ScreenshotStorageKey!));
        }

        var missing = await GetScreenshotAsync(bug.Id, "learner-1");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private async Task<HttpResponseMessage> ReportBugWithScreenshotAsync(string userId)
    {
        var request = new BugReportRequest(
            Screen: "quiz",
            Description: "Screenshot route should be private.",
            StepsToReproduce: "Open bug reports and fetch the attachment.",
            Severity: "medium",
            Platform: "android",
            Locale: "sr-RS",
            AppVersion: "1.0.0",
            ScreenshotBase64: $"data:image/png;base64,{Convert.ToBase64String(MinimalPng)}");

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/bugs/report")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Test-UserId", userId);
        return await client.SendAsync(message);
    }

    private async Task<HttpResponseMessage> GetScreenshotAsync(
        Guid id,
        string userId,
        string? role = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/bugs/{id}/screenshot");
        request.Headers.Add("X-Test-UserId", userId);
        if (!string.IsNullOrWhiteSpace(role))
        {
            request.Headers.Add("X-Test-Roles", role);
        }

        return await client.SendAsync(request);
    }
}

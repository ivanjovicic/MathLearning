using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Bugs;
using MathLearning.Application.Services;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MathLearning.Tests.Endpoints;

public sealed class BugEndpointAuthorizationTests :
    IClassFixture<BugEndpointAuthorizationWebApplicationFactory>,
    IAsyncLifetime
{
    private readonly BugEndpointAuthorizationWebApplicationFactory factory;
    private readonly HttpClient client;

    public BugEndpointAuthorizationTests(BugEndpointAuthorizationWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public Task InitializeAsync()
    {
        factory.BugService.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnonymousUser_CannotSubmitOrReadBugReports()
    {
        var report = await client.PostAsJsonAsync("/api/bugs/report", CreateReportRequest());
        var mine = await client.GetAsync("/api/bugs/mine");
        var adminList = await client.GetAsync("/api/bugs/");
        var adminDetail = await client.GetAsync($"/api/bugs/{factory.BugService.ExistingBugId}");
        var adminUpdate = await client.PatchAsJsonAsync(
            $"/api/bugs/{factory.BugService.ExistingBugId}",
            new UpdateBugStatusRequest("resolved", "admin"));

        AssertUnauthorizedOrForbidden(report.StatusCode);
        AssertUnauthorizedOrForbidden(mine.StatusCode);
        AssertUnauthorizedOrForbidden(adminList.StatusCode);
        AssertUnauthorizedOrForbidden(adminDetail.StatusCode);
        AssertUnauthorizedOrForbidden(adminUpdate.StatusCode);
        Assert.Equal(0, factory.BugService.TotalCalls);
    }

    [Fact]
    public async Task AuthenticatedLearner_CanSubmitAndReadOnlyOwnReports()
    {
        using var reportRequest = AuthenticatedRequest(
            HttpMethod.Post,
            "/api/bugs/report",
            JsonContent.Create(CreateReportRequest()),
            "learner-1");
        var report = await client.SendAsync(reportRequest);

        using var mineRequest = AuthenticatedRequest(
            HttpMethod.Get,
            "/api/bugs/mine?page=0&pageSize=999",
            content: null,
            userId: "learner-1");
        var mine = await client.SendAsync(mineRequest);

        Assert.Equal(HttpStatusCode.Created, report.StatusCode);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Equal("learner-1", factory.BugService.LastCreateUserId);
        Assert.Equal("learner-1", factory.BugService.LastMineUserId);
        Assert.Equal(1, factory.BugService.LastMinePage);
        Assert.Equal(50, factory.BugService.LastMinePageSize);
        Assert.Equal(1, factory.BugService.CreateCalls);
        Assert.Equal(1, factory.BugService.MineCalls);
        Assert.Equal(0, factory.BugService.AdminCalls);
    }

    [Fact]
    public async Task AuthenticatedLearner_CannotListReadOrUpdateAllBugReports()
    {
        using var listRequest = AuthenticatedRequest(HttpMethod.Get, "/api/bugs/", null, "learner-2");
        using var detailRequest = AuthenticatedRequest(
            HttpMethod.Get,
            $"/api/bugs/{factory.BugService.ExistingBugId}",
            null,
            "learner-2");
        using var updateRequest = AuthenticatedRequest(
            HttpMethod.Patch,
            $"/api/bugs/{factory.BugService.ExistingBugId}",
            JsonContent.Create(new UpdateBugStatusRequest("resolved", "learner-2")),
            "learner-2");

        var list = await client.SendAsync(listRequest);
        var detail = await client.SendAsync(detailRequest);
        var update = await client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, detail.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(0, factory.BugService.AdminCalls);
    }

    [Fact]
    public async Task Admin_CanListReadAndUpdateBugReports_WithBoundedPaging()
    {
        using var listRequest = AuthenticatedRequest(
            HttpMethod.Get,
            "/api/bugs/?page=0&pageSize=999&status=open&severity=high",
            null,
            "admin-1",
            DesignTokenSecurity.AdminRole);
        using var detailRequest = AuthenticatedRequest(
            HttpMethod.Get,
            $"/api/bugs/{factory.BugService.ExistingBugId}",
            null,
            "admin-1",
            DesignTokenSecurity.AdminRole);
        using var updateRequest = AuthenticatedRequest(
            HttpMethod.Patch,
            $"/api/bugs/{factory.BugService.ExistingBugId}",
            JsonContent.Create(new UpdateBugStatusRequest("resolved", "admin-1")),
            "admin-1",
            DesignTokenSecurity.AdminRole);

        var list = await client.SendAsync(listRequest);
        var detail = await client.SendAsync(detailRequest);
        var update = await client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal(1, factory.BugService.LastAdminPage);
        Assert.Equal(20, factory.BugService.LastAdminPageSize);
        Assert.Equal("open", factory.BugService.LastAdminStatus);
        Assert.Equal("high", factory.BugService.LastAdminSeverity);
        Assert.Equal(3, factory.BugService.AdminCalls);
    }

    private static BugReportRequest CreateReportRequest() => new(
        Screen: "quiz",
        Description: "The submit button did not respond.",
        StepsToReproduce: "Open quiz and submit an answer.",
        Severity: "medium",
        Platform: "android",
        Locale: "sr-RS",
        AppVersion: "1.0.0",
        ScreenshotBase64: null);

    private static HttpRequestMessage AuthenticatedRequest(
        HttpMethod method,
        string path,
        HttpContent? content,
        string userId,
        string? role = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add("X-Test-UserId", userId);
        if (!string.IsNullOrWhiteSpace(role))
            request.Headers.Add("X-Test-Roles", role);
        return request;
    }

    private static void AssertUnauthorizedOrForbidden(HttpStatusCode statusCode) =>
        Assert.True(
            statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403, got {(int)statusCode}.");
}

public sealed class BugEndpointAuthorizationWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public RecordingBugReportService BugService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBugReportService>();
            services.AddSingleton<IBugReportService>(BugService);
        });
    }
}

public sealed class RecordingBugReportService : IBugReportService
{
    private int createCalls;
    private int mineCalls;
    private int adminCalls;

    public Guid ExistingBugId { get; } = Guid.NewGuid();
    public int CreateCalls => Volatile.Read(ref createCalls);
    public int MineCalls => Volatile.Read(ref mineCalls);
    public int AdminCalls => Volatile.Read(ref adminCalls);
    public int TotalCalls => CreateCalls + MineCalls + AdminCalls;
    public string? LastCreateUserId { get; private set; }
    public string? LastMineUserId { get; private set; }
    public int LastMinePage { get; private set; }
    public int LastMinePageSize { get; private set; }
    public int LastAdminPage { get; private set; }
    public int LastAdminPageSize { get; private set; }
    public string? LastAdminStatus { get; private set; }
    public string? LastAdminSeverity { get; private set; }

    public void Reset()
    {
        Interlocked.Exchange(ref createCalls, 0);
        Interlocked.Exchange(ref mineCalls, 0);
        Interlocked.Exchange(ref adminCalls, 0);
        LastCreateUserId = null;
        LastMineUserId = null;
        LastMinePage = 0;
        LastMinePageSize = 0;
        LastAdminPage = 0;
        LastAdminPageSize = 0;
        LastAdminStatus = null;
        LastAdminSeverity = null;
    }

    public Task<BugReportDto> CreateBugReportAsync(string userId, BugReportRequest request)
    {
        Interlocked.Increment(ref createCalls);
        LastCreateUserId = userId;
        return Task.FromResult(CreateDto(ExistingBugId, userId, request));
    }

    public Task<BugReportsResponse> GetBugReportsAsync(
        int page = 1,
        int pageSize = 20,
        string? status = null,
        string? severity = null)
    {
        Interlocked.Increment(ref adminCalls);
        LastAdminPage = page;
        LastAdminPageSize = pageSize;
        LastAdminStatus = status;
        LastAdminSeverity = severity;
        return Task.FromResult(new BugReportsResponse(
            new List<BugReportDto> { CreateDto(ExistingBugId, "learner-1", CreateDefaultRequest()) },
            1,
            page,
            pageSize));
    }

    public Task<BugReportsResponse> GetMyBugReportsAsync(
        string userId,
        int page = 1,
        int pageSize = 50)
    {
        Interlocked.Increment(ref mineCalls);
        LastMineUserId = userId;
        LastMinePage = page;
        LastMinePageSize = pageSize;
        return Task.FromResult(new BugReportsResponse(
            new List<BugReportDto> { CreateDto(ExistingBugId, userId, CreateDefaultRequest()) },
            1,
            page,
            pageSize));
    }

    public Task<BugReportDto?> GetBugReportAsync(Guid id)
    {
        Interlocked.Increment(ref adminCalls);
        return Task.FromResult<BugReportDto?>(
            id == ExistingBugId
                ? CreateDto(id, "learner-1", CreateDefaultRequest())
                : null);
    }

    public Task<bool> UpdateBugStatusAsync(Guid id, UpdateBugStatusRequest request)
    {
        Interlocked.Increment(ref adminCalls);
        return Task.FromResult(id == ExistingBugId);
    }

    private static BugReportRequest CreateDefaultRequest() => new(
        "quiz",
        "description",
        null,
        "medium",
        "android",
        "sr-RS",
        "1.0.0",
        null);

    private static BugReportDto CreateDto(Guid id, string userId, BugReportRequest request) => new(
        id,
        DateTime.UtcNow,
        userId,
        userId,
        request.Screen,
        request.Description,
        request.StepsToReproduce,
        request.Severity,
        request.Platform,
        request.Locale,
        request.AppVersion,
        null,
        "open",
        null,
        null);
}

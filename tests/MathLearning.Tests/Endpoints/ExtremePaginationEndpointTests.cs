using System.Net;
using System.Text.Json;
using MathLearning.Application.Services;

namespace MathLearning.Tests.Endpoints;

public sealed class AnalyticsExtremePaginationEndpointTests :
    IClassFixture<AnalyticsContractWebApplicationFactory>,
    IAsyncLifetime
{
    private readonly AnalyticsContractWebApplicationFactory factory;
    private readonly HttpClient client;

    public AnalyticsExtremePaginationEndpointTests(AnalyticsContractWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        factory.Service.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Weakness_IntMaxPaging_IsCappedWithoutOverflow()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/analytics/weakness?page={int.MaxValue}&pageSize={int.MaxValue}");
        request.Headers.Add("X-Test-UserId", "analytics-extreme-user");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5_000, factory.Service.LastTake);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(100, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(50, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("returned").GetInt32());
    }
}

public sealed class BugExtremePaginationEndpointTests :
    IClassFixture<BugEndpointAuthorizationWebApplicationFactory>,
    IAsyncLifetime
{
    private readonly BugEndpointAuthorizationWebApplicationFactory factory;
    private readonly HttpClient client;

    public BugExtremePaginationEndpointTests(BugEndpointAuthorizationWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        factory.BugService.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Mine_IntMaxPaging_IsCappedBeforeServiceCall()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/bugs/mine?page={int.MaxValue}&pageSize={int.MaxValue}");
        request.Headers.Add("X-Test-UserId", "bug-extreme-user");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1_000, factory.BugService.LastMinePage);
        Assert.Equal(100, factory.BugService.LastMinePageSize);
    }

    [Fact]
    public async Task AdminList_IntMaxPaging_IsCappedBeforeServiceCall()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/bugs/?page={int.MaxValue}&pageSize={int.MaxValue}");
        request.Headers.Add("X-Test-UserId", "bug-admin");
        request.Headers.Add("X-Test-Roles", DesignTokenSecurity.AdminRole);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1_000, factory.BugService.LastAdminPage);
        Assert.Equal(100, factory.BugService.LastAdminPageSize);
    }
}

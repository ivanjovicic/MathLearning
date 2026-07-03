using System.Net;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Maintenance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MathLearning.Tests.Endpoints;

public sealed class MaintenanceEndpointContractTests :
    IClassFixture<MaintenanceContractWebApplicationFactory>,
    IAsyncLifetime
{
    private readonly MaintenanceContractWebApplicationFactory factory;
    private readonly HttpClient client;

    public MaintenanceEndpointContractTests(MaintenanceContractWebApplicationFactory factory)
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
    public async Task Admin_IndexStats_IsReadOnlyAndNeverInvokesRebuild()
    {
        factory.Service.StatisticsReport = new IndexMaintenanceReport
        {
            BloatedIndexes =
            {
                new IndexBloatInfo
                {
                    IndexName = "IX_Questions_SubtopicId",
                    TableName = "Questions",
                    Size = "2 MB",
                    BloatPercentage = 35m,
                    Scans = 9
                },
                new IndexBloatInfo
                {
                    IndexName = "IX_UserAnswers_UserId",
                    TableName = "UserAnswers",
                    Size = "1 MB",
                    BloatPercentage = 20m,
                    Scans = 100
                }
            },
            UnusedIndexes = { "public.IX_Unused" }
        };

        using var request = AdminRequest(HttpMethod.Get, "/api/maintenance/index-stats");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.Service.StatisticsCalls);
        Assert.Equal(0, factory.Service.RebuildCalls);
        Assert.Equal(0, factory.Service.HealthCalls);
        Assert.True(factory.Service.LastCancellationToken.CanBeCanceled);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = json.RootElement.GetProperty("bloatedIndexes");
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal("NEEDS_REBUILD", rows[0].GetProperty("status").GetString());
        Assert.Equal("WATCH", rows[1].GetProperty("status").GetString());
        Assert.Equal("public.IX_Unused", json.RootElement.GetProperty("unusedIndexes")[0].GetString());
    }

    [Fact]
    public async Task Admin_IndexHealth_ReturnsStableCountsAndCallsOnlyHealthRead()
    {
        factory.Service.HealthRows = new List<IndexHealthInfo>
        {
            new() { IndexName = "healthy", Status = "HEALTHY" },
            new() { IndexName = "unused", Status = "UNUSED" },
            new() { IndexName = "low", Status = "LOW_USAGE" },
            new() { IndexName = "healthy-2", Status = "HEALTHY" }
        };

        using var request = AdminRequest(HttpMethod.Get, "/api/maintenance/index-health");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, factory.Service.StatisticsCalls);
        Assert.Equal(0, factory.Service.RebuildCalls);
        Assert.Equal(1, factory.Service.HealthCalls);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(4, json.RootElement.GetProperty("totalIndexes").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("healthyIndexes").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("unusedIndexes").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("lowUsageIndexes").GetInt32());
    }

    [Fact]
    public async Task Admin_Rebuild_InvokesMutationExactlyOnceAndReturnsReport()
    {
        factory.Service.RebuildReport = new IndexMaintenanceReport
        {
            BloatedIndexes = { new IndexBloatInfo { IndexName = "IX_Bloated" } },
            UnusedIndexes = { "public.IX_Unused" },
            RebuiltIndexes = { "IX_Bloated" }
        };

        using var request = AdminRequest(HttpMethod.Post, "/api/maintenance/rebuild-indexes");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.Service.RebuildCalls);
        Assert.Equal(0, factory.Service.StatisticsCalls);
        Assert.Equal(0, factory.Service.HealthCalls);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Rebuilt 1 indexes", json.RootElement.GetProperty("message").GetString());
        Assert.Equal("IX_Bloated", json.RootElement.GetProperty("rebuiltIndexes")[0].GetString());
    }

    [Fact]
    public async Task Admin_Rebuild_WithItemErrors_ReturnsCompletedReportButSuccessFalse()
    {
        factory.Service.RebuildReport = new IndexMaintenanceReport
        {
            Errors = { "safe maintenance item failure" }
        };

        using var request = AdminRequest(HttpMethod.Post, "/api/maintenance/rebuild-indexes");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("safe maintenance item failure", json.RootElement.GetProperty("errors")[0].GetString());
    }

    private static HttpRequestMessage AdminRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Test-UserId", "maintenance-admin");
        request.Headers.Add("X-Test-Roles", DesignTokenSecurity.AdminRole);
        return request;
    }
}

public sealed class MaintenanceContractWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public RecordingIndexMaintenanceService Service { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIndexMaintenanceService>();
            services.AddSingleton<IIndexMaintenanceService>(Service);
        });
    }
}

public sealed class RecordingIndexMaintenanceService : IIndexMaintenanceService
{
    private int rebuildCalls;
    private int statisticsCalls;
    private int healthCalls;

    public int RebuildCalls => Volatile.Read(ref rebuildCalls);
    public int StatisticsCalls => Volatile.Read(ref statisticsCalls);
    public int HealthCalls => Volatile.Read(ref healthCalls);
    public CancellationToken LastCancellationToken { get; private set; }
    public IndexMaintenanceReport RebuildReport { get; set; } = new();
    public IndexMaintenanceReport StatisticsReport { get; set; } = new();
    public IReadOnlyList<IndexHealthInfo> HealthRows { get; set; } = Array.Empty<IndexHealthInfo>();

    public void Reset()
    {
        Interlocked.Exchange(ref rebuildCalls, 0);
        Interlocked.Exchange(ref statisticsCalls, 0);
        Interlocked.Exchange(ref healthCalls, 0);
        LastCancellationToken = default;
        RebuildReport = new IndexMaintenanceReport();
        StatisticsReport = new IndexMaintenanceReport();
        HealthRows = Array.Empty<IndexHealthInfo>();
    }

    public Task<IndexMaintenanceReport> RebuildCorruptedIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref rebuildCalls);
        LastCancellationToken = cancellationToken;
        return Task.FromResult(RebuildReport);
    }

    public Task<IndexMaintenanceReport> GetIndexStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref statisticsCalls);
        LastCancellationToken = cancellationToken;
        return Task.FromResult(StatisticsReport);
    }

    public Task<IReadOnlyList<IndexHealthInfo>> CheckIndexHealthAsync(
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref healthCalls);
        LastCancellationToken = cancellationToken;
        return Task.FromResult(HealthRows);
    }
}

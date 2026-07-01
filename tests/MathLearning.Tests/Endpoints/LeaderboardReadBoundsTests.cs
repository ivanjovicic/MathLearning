using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Leaderboard;
using MathLearning.Application.Services;
using MathLearning.Core.DTOs;
using MathLearning.Core.Services;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MathLearning.Tests.Endpoints;

public sealed class LeaderboardReadBoundsTests : IClassFixture<LeaderboardReadBoundsWebApplicationFactory>
{
    private readonly HttpClient client;
    private readonly LeaderboardReadBoundsWebApplicationFactory factory;

    public LeaderboardReadBoundsTests(LeaderboardReadBoundsWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLeaderboard_NormalizesInvalidValues_AndClampsLimit()
    {
        using var response = await client.GetAsync("/api/leaderboard?scope=banana&period=banana&limit=0&includeMe=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LeaderboardResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("global", payload!.Scope);
        Assert.Equal("all_time", payload.Period);
        Assert.Single(payload.Items);
        Assert.Equal(1, factory.Spy.LastRedisLeaderboardRequest!.Limit);
        Assert.Equal("global", factory.Spy.LastRedisLeaderboardRequest!.Scope);
        Assert.Equal("all_time", factory.Spy.LastRedisLeaderboardRequest!.Period);
    }

    [Fact]
    public async Task GetStudentLeaderboard_NormalizesInvalidValues_AndClampsLimit()
    {
        using var response = await client.GetAsync("/api/leaderboard/student?scope=banana&period=banana&limit=-25&includeMe=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LeaderboardResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("global", payload!.Scope);
        Assert.Equal("all_time", payload.Period);
        Assert.Single(payload.Items);
        Assert.Equal(1, factory.Spy.LastStudentRequest!.Limit);
        Assert.Equal("global", factory.Spy.LastStudentRequest!.Scope);
        Assert.Equal("all_time", factory.Spy.LastStudentRequest!.Period);
    }

    [Fact]
    public async Task GetSchoolLeaderboard_ClampsLimit_AndNormalizesPeriod()
    {
        using var response = await client.GetAsync("/api/leaderboard/schools?period=invalid&limit=999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<SchoolLeaderboardResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("week", payload!.Period);
        Assert.Equal(200, payload.Items.Count);
        Assert.Equal("week", factory.Spy.LastSchoolListPeriod);
        Assert.Equal(200, factory.Spy.LastSchoolListLimit);
    }

    [Fact]
    public async Task GetSchoolLeaderboardDetail_ClampsNeighbors_AndNormalizesPeriod()
    {
        using var response = await client.GetAsync("/api/leaderboard/schools/1?period=invalid&neighbors=-12");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<SchoolLeaderboardDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal("week", payload!.Period);
        Assert.Single(payload.NearbySchools);
        Assert.Equal("week", factory.Spy.LastDetailPeriod);
        Assert.Equal(1, factory.Spy.LastDetailNeighbors);
    }

    [Fact]
    public async Task GetSchoolLeaderboardHistory_ClampsTake_AndNormalizesPeriod()
    {
        using var response = await client.GetAsync("/api/leaderboard/schools/history/1?period=invalid&take=999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<SchoolLeaderboardHistoryResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("week", payload!.Period);
        Assert.Equal(120, payload.Points.Count);
        Assert.Equal("week", factory.Spy.LastHistoryPeriod);
        Assert.Equal(120, factory.Spy.LastHistoryTake);
    }

    [Fact]
    public async Task GetGlobalLeaderboard_InvalidRange_FallsBackToAllTime()
    {
        using var baseline = await client.GetAsync("/api/leaderboard/global?range=allTime&limit=250");
        using var invalid = await client.GetAsync("/api/leaderboard/global?range=banana&limit=250");

        Assert.Equal(HttpStatusCode.OK, baseline.StatusCode);
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);

        var baselineBody = await baseline.Content.ReadAsStringAsync();
        var invalidBody = await invalid.Content.ReadAsStringAsync();

        Assert.Equal(baselineBody, invalidBody);
    }
}

public sealed class LeaderboardReadBoundsWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public LeaderboardBoundsSpyService Spy { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRedisLeaderboardService>();
            services.RemoveAll<ILeaderboardService>();
            services.RemoveAll<ISchoolLeaderboardService>();
            services.RemoveAll<IStudentLeaderboardService>();

            services.AddSingleton(Spy);
            services.AddSingleton<IRedisLeaderboardService>(Spy);
            services.AddSingleton<ILeaderboardService>(Spy);
            services.AddSingleton<ISchoolLeaderboardService>(Spy);
            services.AddSingleton<IStudentLeaderboardService>(Spy);
        });
    }
}

public sealed class LeaderboardBoundsSpyService :
    IRedisLeaderboardService,
    ILeaderboardService,
    ISchoolLeaderboardService,
    IStudentLeaderboardService
{
    private readonly List<LeaderboardEntryDto> redisEntries = CreateLeaderboardEntries();
    private readonly List<LeaderboardEntryDto> studentEntries = CreateLeaderboardEntries();

    public LeaderboardRequestDto? LastRedisLeaderboardRequest { get; private set; }
    public LeaderboardRequestDto? LastRedisRankRequest { get; private set; }
    public LeaderboardRequestDto? LastStudentRequest { get; private set; }
    public string? LastSchoolListPeriod { get; private set; }
    public int LastSchoolListLimit { get; private set; }
    public string? LastDetailPeriod { get; private set; }
    public int LastDetailNeighbors { get; private set; }
    public string? LastHistoryPeriod { get; private set; }
    public int LastHistoryTake { get; private set; }

    public Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(LeaderboardRequestDto request)
    {
        LastRedisLeaderboardRequest = request;
        return Task.FromResult(redisEntries.Take(request.Limit).ToList());
    }

    public Task<LeaderboardEntryDto?> GetUserRankAsync(LeaderboardRequestDto request)
    {
        LastRedisRankRequest = request;
        return Task.FromResult<LeaderboardEntryDto?>(redisEntries.First());
    }

    public Task<List<LeaderboardEntryDto>> GetNearRivalsAsync(LeaderboardRequestDto request)
    {
        LastRedisRankRequest = request;
        return Task.FromResult(redisEntries.Take(5).ToList());
    }

    public Task UpdateLeaderboardAsync(LeaderboardUpdateDto update)
    {
        _ = update;
        return Task.CompletedTask;
    }

    public Task<SchoolLeaderboardResponseDto> GetSchoolLeaderboardAsync(string userId, string period, int limit, string? cursor = null)
    {
        _ = userId;
        _ = cursor;
        LastSchoolListPeriod = period;
        LastSchoolListLimit = limit;

        var items = Enumerable.Range(1, limit)
            .Select(index => new SchoolLeaderboardItemDto
            {
                Rank = index,
                SchoolId = index,
                SchoolName = $"School {index}",
                Score = 1000 - index,
                Members = 100 + index
            })
            .ToList();

        return Task.FromResult(new SchoolLeaderboardResponseDto
        {
            Period = period,
            Items = items,
            MySchool = items.FirstOrDefault(),
            NextCursor = null
        });
    }

    public Task EnsureCurrentPeriodAsync(string period, CancellationToken ct = default)
    {
        _ = period;
        _ = ct;
        return Task.CompletedTask;
    }

    public Task RefreshCurrentPeriodAsync(string period, CancellationToken ct = default)
    {
        _ = period;
        _ = ct;
        return Task.CompletedTask;
    }

    public Task RefreshAllCurrentPeriodsAsync(CancellationToken ct = default)
    {
        _ = ct;
        return Task.CompletedTask;
    }

    public Task CaptureSnapshotAsync(string period, CancellationToken ct = default)
    {
        _ = period;
        _ = ct;
        return Task.CompletedTask;
    }

    public Task<SchoolLeaderboardDetailDto?> GetSchoolLeaderboardDetailsAsync(int schoolId, string period, int neighbors = 2, CancellationToken ct = default)
    {
        _ = ct;
        LastDetailPeriod = period;
        LastDetailNeighbors = neighbors;

        var nearby = Enumerable.Range(1, neighbors)
            .Select(index => new SchoolLeaderboardItemDto
            {
                Rank = index + 1,
                SchoolId = schoolId + index,
                SchoolName = $"Nearby School {index}",
                Score = 900 - index,
                Members = 80 + index
            })
            .ToList();

        return Task.FromResult<SchoolLeaderboardDetailDto?>(new SchoolLeaderboardDetailDto
        {
            Period = period,
            School = new SchoolLeaderboardItemDto
            {
                Rank = 1,
                SchoolId = schoolId,
                SchoolName = $"School {schoolId}",
                Score = 1200,
                Members = 150
            },
            NearbySchools = nearby
        });
    }

    public Task<SchoolLeaderboardHistoryResponseDto> GetSchoolLeaderboardHistoryAsync(int schoolId, string period, int take = 30, CancellationToken ct = default)
    {
        _ = ct;
        LastHistoryPeriod = period;
        LastHistoryTake = take;

        var points = Enumerable.Range(1, take)
            .Select(index => new SchoolLeaderboardHistoryPointDto
            {
                SnapshotTimeUtc = DateTime.UtcNow.AddDays(-index),
                Rank = index,
                Score = 1000 - index,
                ActiveStudents = 80 + index,
                ParticipationRate = 0.5m,
                CompositeScore = 0.9m
            })
            .ToList();

        return Task.FromResult(new SchoolLeaderboardHistoryResponseDto
        {
            SchoolId = schoolId,
            Period = period,
            Points = points
        });
    }

    public Task<LeaderboardResponseDto> GetLeaderboardAsync(
        string userId,
        string scope,
        string period,
        int limit,
        string? cursor = null,
        bool includeMe = true,
        CancellationToken ct = default)
    {
        _ = userId;
        _ = cursor;
        _ = includeMe;
        _ = ct;
        LastStudentRequest = new LeaderboardRequestDto
        {
            Scope = scope,
            Period = period,
            Limit = limit
        };

        var items = studentEntries.Take(limit)
            .Select((entry, index) => new LeaderboardItemDto
            {
                Rank = index + 1,
                UserId = entry.UserId,
                DisplayName = entry.DisplayName,
                Score = entry.Xp,
                Level = entry.Level,
                StreakDays = entry.Streak
            })
            .ToList();

        return Task.FromResult(new LeaderboardResponseDto
        {
            Scope = scope,
            Period = period,
            Items = items,
            Me = includeMe && items.Count > 0
                ? new LeaderboardMeDto
                {
                    Rank = 1,
                    Score = items[0].Score,
                    Percentile = 100,
                    Badges = []
                }
                : null
        });
    }

    private static List<LeaderboardEntryDto> CreateLeaderboardEntries() =>
        Enumerable.Range(1, 250)
            .Select(index => new LeaderboardEntryDto(
                index,
                $"user-{index:000}",
                $"User {index}",
                1 + (index % 10),
                5000 - index,
                3000 - index,
                index % 20))
            .ToList();
}

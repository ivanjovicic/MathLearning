using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Application.DTOs.Leaderboard;
using MathLearning.Application.DTOs.Progress;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Cosmetics;
using MathLearning.Infrastructure.Services.Leaderboard;
using MathLearning.Infrastructure.Services.Performance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Endpoints;

public sealed class ReadPathMutationRegressionTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ReadPathMutationRegressionTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProgressOverview_DoesNotMutateStreakState()
    {
        var userId = $"progress-read-{Guid.NewGuid():N}";
        var lastStreakDay = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3));
        await EnsureUserAsync(userId, streak: 7, lastStreakDay: lastStreakDay, freezes: 0);

        var before = await LoadProfileAsync(userId);

        var response = await SendGetAsync("/api/progress/overview", userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ProgressOverviewDto>();
        Assert.NotNull(payload);
        Assert.Equal(7, payload!.DailyStreak);
        Assert.Equal(0, payload.StreakFreezeCount);
        Assert.Equal(lastStreakDay, payload.LastStreakDay);
        Assert.Null(payload.StreakEvent);

        var after = await LoadProfileAsync(userId);
        Assert.Equal(before.Streak, after.Streak);
        Assert.Equal(before.StreakFreezeCount, after.StreakFreezeCount);
        Assert.Equal(before.LastStreakDay, after.LastStreakDay);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
    }

    [Fact]
    public async Task SrsStreak_DoesNotMutateStreakState()
    {
        var userId = $"srs-read-{Guid.NewGuid():N}";
        var lastStreakDay = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3));
        await EnsureUserAsync(userId, streak: 5, lastStreakDay: lastStreakDay, freezes: 0);

        var before = await LoadProfileAsync(userId);

        var response = await SendGetAsync("/api/quiz/srs/streak", userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        if (payload.TryGetProperty("streakEvent", out var streakEvent))
        {
            Assert.Equal(JsonValueKind.Null, streakEvent.ValueKind);
        }
        Assert.Equal(5, payload.GetProperty("streak").GetInt32());
        Assert.Equal(0, payload.GetProperty("streakFreezeCount").GetInt32());
        Assert.Equal(lastStreakDay.ToString("O"), payload.GetProperty("lastStreakDay").GetString());

        var after = await LoadProfileAsync(userId);
        Assert.Equal(before.Streak, after.Streak);
        Assert.Equal(before.StreakFreezeCount, after.StreakFreezeCount);
        Assert.Equal(before.LastStreakDay, after.LastStreakDay);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
    }

    [Fact]
    public async Task SchoolLeaderboard_DoesNotRefreshOrGrantRewards()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var periodInfo = SchoolLeaderboardPeriods.Normalize("week");
        var staleUpdatedAt = DateTime.UtcNow.AddMinutes(-10);

        db.Schools.Add(new School { Id = 101, Name = "Read School" });
        var profile = await db.UserProfiles.SingleAsync(x => x.UserId == "1");
        profile.SchoolId = 101;
        profile.LeaderboardOptIn = true;
        profile.WeeklyXp = 250;

        db.SchoolScoreAggregates.Add(new SchoolScoreAggregate
        {
            SchoolId = 101,
            Period = periodInfo.Period,
            PeriodStartUtc = periodInfo.PeriodStartUtc,
            XpTotal = 500,
            ActiveStudents = 1,
            EligibleStudents = 1,
            AverageXpPerActiveStudent = 500m,
            ParticipationRate = 1m,
            CompositeScore = 99m,
            Rank = 1,
            UpdatedAtUtc = staleUpdatedAt
        });
        await db.SaveChangesAsync();

        var rewardSpy = new RecordingCosmeticRewardService();
        var sut = new LeaderboardService(db, NullLogger<LeaderboardService>.Instance, rewardSpy);

        var result = await sut.GetSchoolLeaderboardAsync("1", "week", 10);

        Assert.NotNull(result.MySchool);
        Assert.Single(result.Items);
        Assert.True(result.IsStale);
        Assert.Equal(0, rewardSpy.ProcessRewardSourceCalls);

        var stored = await db.SchoolScoreAggregates
            .Where(x => x.SchoolId == 101 && x.Period == periodInfo.Period && x.PeriodStartUtc == periodInfo.PeriodStartUtc)
            .Select(x => x.UpdatedAtUtc)
            .SingleAsync();
        Assert.Equal(staleUpdatedAt, stored);
    }

    [Fact]
    public async Task SchoolLeaderboardHistory_DoesNotCreateSnapshotRowsWhenEmpty()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var periodInfo = SchoolLeaderboardPeriods.Normalize("week");

        db.Schools.Add(new School { Id = 202, Name = "History School" });
        db.SchoolScoreAggregates.Add(new SchoolScoreAggregate
        {
            SchoolId = 202,
            Period = periodInfo.Period,
            PeriodStartUtc = periodInfo.PeriodStartUtc,
            XpTotal = 700,
            ActiveStudents = 7,
            EligibleStudents = 9,
            AverageXpPerActiveStudent = 100m,
            ParticipationRate = 0.78m,
            CompositeScore = 88m,
            Rank = 1,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new LeaderboardService(db, NullLogger<LeaderboardService>.Instance);
        var result = await sut.GetSchoolLeaderboardHistoryAsync(202, "week", 10);

        Assert.Empty(result.Points);
        Assert.Equal(0, await db.SchoolRankHistories.CountAsync());
    }

    [Fact]
    public async Task StudentLeaderboard_DoesNotGrantRewards()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var rewardSpy = new RecordingCosmeticRewardService();
        var cache = new HybridCacheService(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 }),
            NullLogger<HybridCacheService>.Instance);
        var appearanceReader = new AvatarAppearanceReader(db);
        var sut = new StudentLeaderboardService(
            db,
            NullLogger<StudentLeaderboardService>.Instance,
            cache,
            appearanceReader,
            rewardSpy);

        var result = await sut.GetLeaderboardAsync("1", "global", "all_time", 10, includeMe: true);

        Assert.NotNull(result.Me);
        Assert.Equal(0, rewardSpy.ProcessRewardSourceCalls);
        Assert.Equal(0, rewardSpy.ProcessProgressRewardsCalls);
        Assert.Equal(0, rewardSpy.ClaimRewardTrackTierCalls);
    }

    private async Task EnsureUserAsync(string userId, int streak, DateOnly? lastStreakDay, int freezes)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        if (!await db.Users.AnyAsync(x => x.Id == userId))
        {
            db.Users.Add(new IdentityUser
            {
                Id = userId,
                UserName = userId,
                Email = $"{userId}@example.test"
            });
        }

        var profile = await db.UserProfiles.SingleOrDefaultAsync(x => x.UserId == userId);
        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                Username = userId,
                DisplayName = userId,
                Coins = 100,
                Level = 1,
                Xp = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.UserProfiles.Add(profile);
        }

        profile.Streak = streak;
        profile.StreakFreezeCount = freezes;
        profile.LastStreakDay = lastStreakDay;
        profile.LastActivityDay = lastStreakDay;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.LeaderboardOptIn = true;

        await db.SaveChangesAsync();
    }

    private async Task<UserProfile> LoadProfileAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserProfiles.AsNoTracking().SingleAsync(x => x.UserId == userId);
    }

    private async Task<HttpResponseMessage> SendGetAsync(string path, string userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-UserId", userId);
        return await _client.SendAsync(request);
    }

    private sealed class RecordingCosmeticRewardService : ICosmeticRewardService
    {
        public int ProcessProgressRewardsCalls { get; private set; }
        public int ProcessRewardSourceCalls { get; private set; }
        public int ClaimRewardTrackTierCalls { get; private set; }

        public Task<IReadOnlyList<CosmeticUnlockResultDto>> ProcessProgressRewardsAsync(string userId, CancellationToken cancellationToken)
        {
            _ = userId;
            _ = cancellationToken;
            ProcessProgressRewardsCalls++;
            return Task.FromResult<IReadOnlyList<CosmeticUnlockResultDto>>(Array.Empty<CosmeticUnlockResultDto>());
        }

        public Task<IReadOnlyList<CosmeticUnlockResultDto>> ProcessRewardSourceAsync(CosmeticRewardSourceRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            ProcessRewardSourceCalls++;
            return Task.FromResult<IReadOnlyList<CosmeticUnlockResultDto>>(Array.Empty<CosmeticUnlockResultDto>());
        }

        public Task<ClaimRewardTrackTierResponse> ClaimRewardTrackTierAsync(string userId, ClaimRewardTrackTierRequest request, CancellationToken cancellationToken)
        {
            _ = userId;
            _ = request;
            _ = cancellationToken;
            ClaimRewardTrackTierCalls++;
            return Task.FromResult(new ClaimRewardTrackTierResponse(false, false, 0, string.Empty, 0, Array.Empty<CosmeticUnlockResultDto>()));
        }
    }
}

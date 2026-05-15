using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class DailyRunEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public DailyRunEndpointsIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FirstClaim_AwardsOnce_AndStoresClaimRecord()
    {
        var day = new DateOnly(2026, 05, 15);
        await EnsureUserAndCompletionAsync("test-user", day, completed: true);
        var before = await GetProfileAsync("test-user");

        var response = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            transactionId = "daily_chest_tx_first_award",
            date = "2026-05-15"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.False(payload.GetProperty("alreadyClaimed").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var claimsCount = await db.DailyRunChestClaims.CountAsync(x => x.UserId == "test-user" && x.Day == day);
        Assert.Equal(1, claimsCount);

        var after = await GetProfileAsync("test-user");
        Assert.True(after.Xp > before.Xp);
        Assert.True(after.Coins > before.Coins);
    }

    [Fact]
    public async Task SameTransactionId_Retry_ReturnsIdenticalPayload_AndNoDuplicateAward()
    {
        var day = new DateOnly(2026, 05, 16);
        await EnsureUserAndCompletionAsync("test-user", day, completed: true);
        var before = await GetProfileAsync("test-user");

        var request = new
        {
            transactionId = "daily_chest_tx_retry",
            date = "2026-05-16"
        };

        var first = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", request);
        var firstBody = await first.Content.ReadAsStringAsync();
        var afterFirst = await GetProfileAsync("test-user");

        var second = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", request);
        var secondBody = await second.Content.ReadAsStringAsync();
        var afterSecond = await GetProfileAsync("test-user");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstBody, secondBody);

        Assert.True(afterFirst.Xp > before.Xp);
        Assert.Equal(afterFirst.Xp, afterSecond.Xp);
        Assert.Equal(afterFirst.Coins, afterSecond.Coins);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var claimsCount = await db.DailyRunChestClaims.CountAsync(x => x.UserId == "test-user" && x.Day == day);
        Assert.Equal(1, claimsCount);
    }

    [Fact]
    public async Task NewTransactionId_OnAlreadyClaimedDate_ReturnsExistingSnapshot_NoSecondAward()
    {
        var day = new DateOnly(2026, 05, 17);
        await EnsureUserAndCompletionAsync("test-user", day, completed: true);

        var first = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            transactionId = "daily_chest_tx_original",
            date = "2026-05-17"
        });
        var firstBody = await first.Content.ReadAsStringAsync();
        var afterFirst = await GetProfileAsync("test-user");

        var second = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            transactionId = "daily_chest_tx_new1",
            date = "2026-05-17"
        });
        var secondBody = await second.Content.ReadAsStringAsync();
        var afterSecond = await GetProfileAsync("test-user");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstBody, secondBody);
        Assert.Equal(afterFirst.Xp, afterSecond.Xp);
        Assert.Equal(afterFirst.Coins, afterSecond.Coins);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var claimsCount = await db.DailyRunChestClaims.CountAsync(x => x.UserId == "test-user" && x.Day == day);
        Assert.Equal(1, claimsCount);
    }

    [Fact]
    public async Task ConcurrentClaims_ResultInSingleAward()
    {
        var day = new DateOnly(2026, 05, 18);
        await EnsureUserAndCompletionAsync("test-user", day, completed: true);
        var before = await GetProfileAsync("test-user");

        var t1 = _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            transactionId = "daily_chest_tx_concurrent_1",
            date = "2026-05-18"
        });
        var t2 = _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            transactionId = "daily_chest_tx_concurrent_2",
            date = "2026-05-18"
        });

        var responses = await Task.WhenAll(t1, t2);
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

        var after = await GetProfileAsync("test-user");
        Assert.True(after.Xp > before.Xp);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var claimsCount = await db.DailyRunChestClaims.CountAsync(x => x.UserId == "test-user" && x.Day == day);
        Assert.Equal(1, claimsCount);
    }

    [Fact]
    public async Task UserCannotClaimAnotherUsersReward_ByPassingUserIdInBody()
    {
        var day = new DateOnly(2026, 05, 19);
        await EnsureUserAndCompletionAsync("victim-user", day, completed: true);

        var response = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            transactionId = "daily_chest_tx_hijack_attempt",
            date = "2026-05-19",
            userId = "victim-user"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DAILY_RUN_NOT_COMPLETED", payload.GetProperty("code").GetString());
    }

    private async Task EnsureUserAndCompletionAsync(string userId, DateOnly day, bool completed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var identity = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (identity is null)
        {
            db.Users.Add(new IdentityUser
            {
                Id = userId,
                UserName = userId,
                Email = $"{userId}@example.test"
            });
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile is null)
        {
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Username = userId,
                DisplayName = userId,
                Coins = 100,
                Level = 1,
                Xp = 0,
                Streak = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        var stat = await db.UserDailyStats.FirstOrDefaultAsync(x => x.UserId == userId && x.Day == day);
        if (stat is null)
        {
            db.UserDailyStats.Add(new UserDailyStat
            {
                UserId = userId,
                Day = day,
                Completed = completed
            });
        }
        else
        {
            stat.Completed = completed;
        }

        await db.SaveChangesAsync();
    }

    private async Task<UserProfile> GetProfileAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        Assert.NotNull(profile);
        return profile!;
    }
}

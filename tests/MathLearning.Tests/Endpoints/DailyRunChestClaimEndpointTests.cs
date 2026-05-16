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

public sealed class DailyRunChestClaimEndpointTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public DailyRunChestClaimEndpointTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task InvalidDate_Returns400_WithInvalidDateCode()
    {
        var response = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            transactionId = "tx-invalid-date",
            date = "2026/05/15"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_DATE", payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task MissingTransactionId_Returns400_WithInvalidTransactionCode()
    {
        var response = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            date = "2026-05-15"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_TRANSACTION_ID", payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task IncompleteDailyRun_Returns409_WithNotCompletedCode()
    {
        var day = new DateOnly(2026, 05, 15);
        await EnsureUserStateAsync("test-user", day, completed: false);

        var response = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            transactionId = "tx-not-completed",
            date = "2026-05-15"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DAILY_RUN_NOT_COMPLETED", payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task FirstValidClaim_ReturnsSuccess_AndBalances()
    {
        var day = new DateOnly(2026, 05, 16);
        await EnsureUserStateAsync("test-user", day, completed: true);
        var before = await GetProfileAsync("test-user");

        var response = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            transactionId = "tx-first-valid",
            date = "2026-05-16"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.False(payload.GetProperty("alreadyClaimed").GetBoolean());

        var reward = payload.GetProperty("reward");
        Assert.True(reward.GetProperty("xp").GetInt32() > 0);
        Assert.True(reward.GetProperty("coins").GetInt32() > 0);
        Assert.False(string.IsNullOrWhiteSpace(reward.GetProperty("cosmeticFragment").GetString()));

        var balances = payload.GetProperty("balances");
        Assert.True(balances.GetProperty("xp").GetInt32() >= before.Xp);
        Assert.True(balances.GetProperty("coins").GetInt32() >= before.Coins);
    }

    [Fact]
    public async Task SameTransaction_Retry_ReturnsAlreadyClaimedTrue_AndSameReward()
    {
        var day = new DateOnly(2026, 05, 17);
        await EnsureUserStateAsync("test-user", day, completed: true);

        var request = new
        {
            transactionId = "tx-same-transaction",
            date = "2026-05-17"
        };

        var first = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", request);
        var firstPayload = await first.Content.ReadFromJsonAsync<JsonElement>();

        var second = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", request);
        var secondPayload = await second.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.False(firstPayload.GetProperty("alreadyClaimed").GetBoolean());
        Assert.True(secondPayload.GetProperty("alreadyClaimed").GetBoolean());

        Assert.Equal(
            firstPayload.GetProperty("reward").ToString(),
            secondPayload.GetProperty("reward").ToString());

        Assert.Equal(
            firstPayload.GetProperty("balances").ToString(),
            secondPayload.GetProperty("balances").ToString());
    }

    [Fact]
    public async Task DifferentTransaction_SameDay_DoesNotDuplicateAward()
    {
        var day = new DateOnly(2026, 05, 18);
        await EnsureUserStateAsync("test-user", day, completed: true);
        var before = await GetProfileAsync("test-user");

        var first = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            transactionId = "tx-day-duplicate-first",
            date = "2026-05-18"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var afterFirst = await GetProfileAsync("test-user");

        var second = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            transactionId = "tx-day-duplicate-second",
            date = "2026-05-18"
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var secondPayload = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(secondPayload.GetProperty("alreadyClaimed").GetBoolean());

        var afterSecond = await GetProfileAsync("test-user");
        Assert.True(afterFirst.Xp > before.Xp);
        Assert.Equal(afterFirst.Xp, afterSecond.Xp);
        Assert.Equal(afterFirst.Coins, afterSecond.Coins);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var claimsCount = await db.DailyRunChestClaims.CountAsync(x => x.UserId == "test-user" && x.Day == day);
        Assert.Equal(1, claimsCount);
    }

    [Fact]
    public async Task DifferentDay_CanClaimSeparately_AfterCompletion()
    {
        var firstDay = new DateOnly(2026, 05, 19);
        var secondDay = new DateOnly(2026, 05, 20);
        await EnsureUserStateAsync("test-user", firstDay, completed: true);
        await EnsureUserStateAsync("test-user", secondDay, completed: true);

        var first = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            transactionId = "tx-different-day-1",
            date = "2026-05-19"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var afterFirst = await GetProfileAsync("test-user");

        var second = await _client.PostAsJsonAsync("/api/daily-run/chest/claim", new
        {
            transactionId = "tx-different-day-2",
            date = "2026-05-20"
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var secondPayload = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(secondPayload.GetProperty("alreadyClaimed").GetBoolean());

        var afterSecond = await GetProfileAsync("test-user");
        Assert.True(afterSecond.Xp > afterFirst.Xp);
        Assert.True(afterSecond.Coins > afterFirst.Coins);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var claimsCount = await db.DailyRunChestClaims.CountAsync(x =>
            x.UserId == "test-user" &&
            (x.Day == firstDay || x.Day == secondDay));
        Assert.Equal(2, claimsCount);
    }

    private async Task EnsureUserStateAsync(string userId, DateOnly day, bool completed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var existingClaims = await db.DailyRunChestClaims
            .Where(x => x.UserId == userId && x.Day == day)
            .ToListAsync();
        if (existingClaims.Count > 0)
        {
            db.DailyRunChestClaims.RemoveRange(existingClaims);
        }

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
        var profile = await db.UserProfiles.AsNoTracking().FirstAsync(x => x.UserId == userId);
        return profile;
    }
}

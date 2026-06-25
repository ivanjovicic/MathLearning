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

namespace MathLearning.Tests.Idempotency;

/// <summary>
/// Domain-table idempotency contract for POST /api/daily-run/chest/claim (Policy B).
/// </summary>
public sealed class DailyRunChestClaimIdempotencyTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public DailyRunChestClaimIdempotencyTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FirstSuccess_AppliesRewardOnce()
    {
        var userId = NewUserId("first");
        var day = new DateOnly(2026, 6, 10);
        await EnsureUserStateAsync(userId, day, completed: true);
        var before = await GetProfileAsync(userId);

        var response = await PostClaimAsync(userId, "tx-first", day);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(payload.GetProperty("alreadyClaimed").GetBoolean());

        var after = await GetProfileAsync(userId);
        Assert.True(after.Xp > before.Xp);
        Assert.True(after.Coins > before.Coins);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(1, await db.DailyRunChestClaims.CountAsync(x => x.UserId == userId));
    }

    [Fact]
    public async Task DuplicateSameTransaction_ReturnsAlreadyClaimed_WithoutDoubleAward()
    {
        var userId = NewUserId("dup-tx");
        var day = new DateOnly(2026, 6, 11);
        await EnsureUserStateAsync(userId, day, completed: true);

        var request = new { transactionId = "tx-dup", date = "2026-06-11" };
        var first = await PostClaimAsync(userId, request);
        var second = await PostClaimAsync(userId, request);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var secondPayload = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(secondPayload.GetProperty("alreadyClaimed").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(1, await db.DailyRunChestClaims.CountAsync(x => x.UserId == userId && x.Day == day));
    }

    [Fact]
    public async Task SameTransaction_DifferentDate_ReturnsOriginalClaim_NoSecondAward()
    {
        var userId = NewUserId("tx-cross-day");
        var firstDay = new DateOnly(2026, 6, 12);
        var secondDay = new DateOnly(2026, 6, 13);
        const string transactionId = "tx-cross-day";
        await EnsureUserStateAsync(userId, firstDay, completed: true);
        await EnsureUserStateAsync(userId, secondDay, completed: true);
        var before = await GetProfileAsync(userId);

        var first = await PostClaimAsync(userId, transactionId, firstDay);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostClaimAsync(userId, transactionId, secondDay);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondPayload = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(secondPayload.GetProperty("alreadyClaimed").GetBoolean());
        Assert.Equal("2026-06-12", secondPayload.GetProperty("date").GetString());

        var after = await GetProfileAsync(userId);
        Assert.True(after.Xp > before.Xp);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(1, await db.DailyRunChestClaims.CountAsync(x => x.UserId == userId));
        Assert.Equal(0, await db.DailyRunChestClaims.CountAsync(x => x.UserId == userId && x.Day == secondDay));
    }

    [Fact]
    public async Task DifferentTransaction_SameDay_ReturnsAlreadyClaimed_NoDoubleAward()
    {
        var userId = NewUserId("day-dup");
        var day = new DateOnly(2026, 6, 14);
        await EnsureUserStateAsync(userId, day, completed: true);
        var before = await GetProfileAsync(userId);

        var first = await PostClaimAsync(userId, "tx-day-a", day);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var afterFirst = await GetProfileAsync(userId);

        var second = await PostClaimAsync(userId, "tx-day-b", day);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondPayload = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(secondPayload.GetProperty("alreadyClaimed").GetBoolean());

        var afterSecond = await GetProfileAsync(userId);
        Assert.Equal(afterFirst.Xp, afterSecond.Xp);
        Assert.Equal(afterFirst.Coins, afterSecond.Coins);
        Assert.True(afterFirst.Xp > before.Xp);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(1, await db.DailyRunChestClaims.CountAsync(x => x.UserId == userId && x.Day == day));
    }

    [Fact]
    public async Task ConcurrentClaims_ResultInSingleAward()
    {
        var userId = NewUserId("concurrent");
        var day = new DateOnly(2026, 6, 15);
        await EnsureUserStateAsync(userId, day, completed: true);
        var before = await GetProfileAsync(userId);

        var t1 = PostClaimAsync(userId, "tx-concurrent-1", day);
        var t2 = PostClaimAsync(userId, "tx-concurrent-2", day);
        var responses = await Task.WhenAll(t1, t2);

        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

        var after = await GetProfileAsync(userId);
        Assert.True(after.Xp > before.Xp);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(1, await db.DailyRunChestClaims.CountAsync(x => x.UserId == userId && x.Day == day));
    }

    [Fact]
    public async Task SameTransactionId_DifferentUsers_AreIsolated()
    {
        var userA = NewUserId("user-a");
        var userB = NewUserId("user-b");
        var day = new DateOnly(2026, 6, 16);
        const string transactionId = "shared-tx-id";
        await EnsureUserStateAsync(userA, day, completed: true);
        await EnsureUserStateAsync(userB, day, completed: true);

        Assert.Equal(HttpStatusCode.OK, (await PostClaimAsync(userA, transactionId, day)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostClaimAsync(userB, transactionId, day)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(2, await db.DailyRunChestClaims.CountAsync(x => x.TransactionId == transactionId));
        Assert.Equal(1, await db.DailyRunChestClaims.CountAsync(x => x.UserId == userA));
        Assert.Equal(1, await db.DailyRunChestClaims.CountAsync(x => x.UserId == userB));
    }

    private static string NewUserId(string prefix) => $"daily-chest-idem-{prefix}-{Guid.NewGuid():N}";

    private Task<HttpResponseMessage> PostClaimAsync(string userId, string transactionId, DateOnly day)
    {
        return PostClaimAsync(userId, new
        {
            transactionId,
            date = day.ToString("yyyy-MM-dd"),
            idempotencyKey = $"daily_chest/{userId}/{day:yyyy-MM-dd}"
        });
    }

    private async Task<HttpResponseMessage> PostClaimAsync(string userId, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/daily-run/chest/claim")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await _client.SendAsync(request);
    }

    private async Task EnsureUserStateAsync(string userId, DateOnly day, bool completed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        if (await db.Users.FirstOrDefaultAsync(x => x.Id == userId) is null)
        {
            db.Users.Add(new IdentityUser
            {
                Id = userId,
                UserName = userId,
                Email = $"{userId}@example.test"
            });
        }

        if (await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId) is null)
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
        return await db.UserProfiles.AsNoTracking().FirstAsync(x => x.UserId == userId);
    }
}

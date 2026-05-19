using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class EconomySettlementEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public EconomySettlementEndpointsIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CoinSpend_Success_AndRetryDoesNotDoubleSpend()
    {
        var userId = $"user-coins-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 120);

        var first = await PostAsUserAsync(userId, "/api/economy/coins/spend", new
        {
            idempotencyKey = "coins-spend-1",
            amount = 10,
            reason = "hint"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var retry = await PostAsUserAsync(userId, "/api/economy/coins/spend", new
        {
            idempotencyKey = "coins-spend-1",
            amount = 10,
            reason = "hint"
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        var retryPayload = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(retryPayload.GetProperty("alreadyProcessed").GetBoolean());
        Assert.Equal(110, await GetCoinsAsync(userId));
    }

    [Fact]
    public async Task CoinSpend_InsufficientBalance_DoesNotMutate()
    {
        var userId = $"user-coins-low-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 5);

        var response = await PostAsUserAsync(userId, "/api/economy/coins/spend", new
        {
            idempotencyKey = "coins-low-1",
            amount = 10,
            reason = "shop"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("insufficient_balance", payload.GetProperty("errorCode").GetString());
        Assert.Equal(5, await GetCoinsAsync(userId));
    }

    [Fact]
    public async Task HintUse_Success_AndRetryDoesNotDoubleSpend()
    {
        var userId = $"user-hint-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 100);

        var first = await PostAsUserAsync(userId, "/api/economy/hints/use", new
        {
            idempotencyKey = "hint-1",
            questionId = 1,
            hintType = "clue",
            costCoins = 999
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var firstPayload = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(firstPayload.GetProperty("usedFreeHint").GetBoolean());
        Assert.Equal(0, firstPayload.GetProperty("spentCoins").GetInt32());

        var retry = await PostAsUserAsync(userId, "/api/economy/hints/use", new
        {
            idempotencyKey = "hint-1",
            questionId = 1,
            hintType = "clue",
            costCoins = 10
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryPayload = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(retryPayload.GetProperty("alreadyProcessed").GetBoolean());
        Assert.Equal(100, await GetCoinsAsync(userId));
    }

    [Fact]
    public async Task CoinsAndHints_AreUserIsolated()
    {
        var key = "same-idempotency";
        var userA = $"user-a-{Guid.NewGuid():N}";
        var userB = $"user-b-{Guid.NewGuid():N}";
        await EnsureUserAsync(userA, coins: 100);
        await EnsureUserAsync(userB, coins: 100);

        var a = await PostAsUserAsync(userA, "/api/economy/coins/spend", new { idempotencyKey = key, amount = 10, reason = "other" });
        var b = await PostAsUserAsync(userB, "/api/economy/coins/spend", new { idempotencyKey = key, amount = 10, reason = "other" });

        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal(HttpStatusCode.OK, b.StatusCode);
        Assert.Equal(90, await GetCoinsAsync(userA));
        Assert.Equal(90, await GetCoinsAsync(userB));
    }

    [Fact]
    public async Task RewardClaim_Success_DuplicateRewardIdBlocked_NotEligibleNoMutation()
    {
        var userId = $"user-reward-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 30, xp: 0);

        var first = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-key-1",
            rewardId = "daily:abc",
            rewardType = "daily",
            coins = 20,
            xp = 15
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var duplicateRewardId = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-key-2",
            rewardId = "daily:abc",
            rewardType = "daily",
            coins = 20,
            xp = 15
        });
        Assert.Equal(HttpStatusCode.OK, duplicateRewardId.StatusCode);
        var dupPayload = await duplicateRewardId.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(dupPayload.GetProperty("alreadyClaimed").GetBoolean());

        await SetRewardEligibilityAsync(userId, "blocked:reward", eligible: false);
        var beforeCoins = await GetCoinsAsync(userId);

        var blocked = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-key-3",
            rewardId = "blocked:reward",
            rewardType = "generic",
            coins = 999,
            xp = 0
        });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var blockedPayload = await blocked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_eligible", blockedPayload.GetProperty("errorCode").GetString());
        Assert.Equal(beforeCoins, await GetCoinsAsync(userId));
    }

    [Fact]
    public async Task StreakFreezePurchase_Success_Insufficient_RetryIdempotent()
    {
        var richUser = $"user-freeze-rich-{Guid.NewGuid():N}";
        await EnsureUserAsync(richUser, coins: 200, streakFreezes: 0);

        var first = await PostAsUserAsync(richUser, "/api/shop/streak-freeze/purchase", new
        {
            idempotencyKey = "freeze-key-1",
            quantity = 1
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(1, await GetStreakFreezeCountAsync(richUser));

        var retry = await PostAsUserAsync(richUser, "/api/shop/streak-freeze/purchase", new
        {
            idempotencyKey = "freeze-key-1",
            quantity = 1
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(1, await GetStreakFreezeCountAsync(richUser));

        var poorUser = $"user-freeze-poor-{Guid.NewGuid():N}";
        await EnsureUserAsync(poorUser, coins: 10, streakFreezes: 0);
        var insufficient = await PostAsUserAsync(poorUser, "/api/shop/streak-freeze/purchase", new
        {
            idempotencyKey = "freeze-key-2",
            quantity = 1
        });
        Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode);
    }

    [Fact]
    public async Task SeasonDailyRunClaim_Success_RetryNoDuplicate_InvalidSeasonRejected()
    {
        var userId = $"user-season-daily-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 100);
        var seasonId = await EnsureActiveSeasonAsync();
        await SeedDailyRunChestClaimAsync(userId, transactionId: "daily-run-tx-1", xp: 25);

        var first = await PostAsUserAsync(userId, "/api/seasons/daily-run-claim", new
        {
            idempotencyKey = "season-daily-key-1",
            transactionId = "daily-run-tx-1",
            seasonId,
            xp = 999
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var firstPayload = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(25, firstPayload.GetProperty("awardedXp").GetInt32());

        var retry = await PostAsUserAsync(userId, "/api/seasons/daily-run-claim", new
        {
            idempotencyKey = "season-daily-key-1",
            transactionId = "daily-run-tx-1",
            seasonId,
            xp = 999
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        var progress = await GetSeasonProgressAsync(userId, seasonId);
        Assert.Equal(25, progress.EarnedXp);

        var invalid = await PostAsUserAsync(userId, "/api/seasons/daily-run-claim", new
        {
            idempotencyKey = "season-daily-key-2",
            transactionId = "missing-tx",
            seasonId = 999999,
            xp = 25
        });
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
    }

    [Fact]
    public async Task SeasonMilestone_ClaimFlow_WorksAndIsIdempotent()
    {
        var userId = $"user-season-ms-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 0);
        var seasonId = await EnsureActiveSeasonAsync();
        var milestoneId = await EnsureSeasonMilestoneAsync(seasonId, xpRequired: 50, rewardType: "coins", payloadJson: """{"coins":50}""");

        var insufficient = await PostAsUserAsync(userId, $"/api/seasons/milestones/{milestoneId}/claim", new
        {
            idempotencyKey = "ms-key-1",
            seasonId
        });
        Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode);

        await SetSeasonXpAsync(userId, seasonId, earnedXp: 60);

        var success = await PostAsUserAsync(userId, $"/api/seasons/milestones/{milestoneId}/claim", new
        {
            idempotencyKey = "ms-key-2",
            seasonId
        });
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);

        var duplicate = await PostAsUserAsync(userId, $"/api/seasons/milestones/{milestoneId}/claim", new
        {
            idempotencyKey = "ms-key-3",
            seasonId
        });
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var dupPayload = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(dupPayload.GetProperty("alreadyClaimed").GetBoolean());
    }

    [Fact]
    public async Task CosmeticItemClaim_AndAlreadyOwned_AreSafe()
    {
        var userId = $"user-item-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 100);
        var itemId = await EnsureCosmeticItemAsync("item-claim-key", "Item Claim");

        var first = await PostAsUserAsync(userId, $"/api/cosmetics/items/{itemId}/claim", new
        {
            idempotencyKey = "item-key-1",
            source = "reward"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAsUserAsync(userId, $"/api/cosmetics/items/{itemId}/claim", new
        {
            idempotencyKey = "item-key-2",
            source = "reward"
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondPayload = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(secondPayload.GetProperty("alreadyOwned").GetBoolean());
    }

    [Fact]
    public async Task CosmeticFragmentGrant_RetryNoDuplicate_UnlocksOnce()
    {
        var userId = $"user-fragment-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 100);
        var unlockItemId = await EnsureCosmeticItemAsync("comet-frame", "Comet Frame");

        var grant = await PostAsUserAsync(userId, "/api/cosmetics/fragments/grant", new
        {
            idempotencyKey = "frag-key-1",
            fragmentName = "Comet Frame Fragment",
            copies = 5,
            source = "dailyRun"
        });
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);

        var retry = await PostAsUserAsync(userId, "/api/cosmetics/fragments/grant", new
        {
            idempotencyKey = "frag-key-1",
            fragmentName = "Comet Frame Fragment",
            copies = 5,
            source = "dailyRun"
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryPayload = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(retryPayload.GetProperty("alreadyProcessed").GetBoolean());

        var secondGrant = await PostAsUserAsync(userId, "/api/cosmetics/fragments/grant", new
        {
            idempotencyKey = "frag-key-2",
            fragmentName = "Comet Frame Fragment",
            copies = 1,
            source = "dailyRun"
        });
        Assert.Equal(HttpStatusCode.OK, secondGrant.StatusCode);

        var ownedCount = await CountOwnedItemAsync(userId, unlockItemId);
        Assert.Equal(1, ownedCount);
    }

    private async Task<HttpResponseMessage> PostAsUserAsync(string userId, string url, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await _client.SendAsync(request);
    }

    private async Task EnsureUserAsync(string userId, int coins, int xp = 0, int streakFreezes = 0)
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
            profile = new UserProfile
            {
                UserId = userId,
                Username = userId,
                DisplayName = userId
            };
            db.UserProfiles.Add(profile);
        }

        profile.Coins = coins;
        profile.Xp = xp;
        profile.Level = 1 + (xp / 100);
        profile.StreakFreezeCount = streakFreezes;
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    private async Task<int> GetCoinsAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserProfiles.Where(x => x.UserId == userId).Select(x => x.Coins).FirstAsync();
    }

    private async Task<int> GetStreakFreezeCountAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserProfiles.Where(x => x.UserId == userId).Select(x => x.StreakFreezeCount).FirstAsync();
    }

    private async Task SetRewardEligibilityAsync(string userId, string rewardId, bool eligible)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var state = await db.UserRewardStates.FirstOrDefaultAsync(x => x.UserId == userId && x.RewardKey == rewardId);
        if (state is null)
        {
            state = new UserRewardState
            {
                UserId = userId,
                RewardKey = rewardId
            };
            db.UserRewardStates.Add(state);
        }

        state.Eligible = eligible;
        state.Claimed = false;
        state.ClaimedAtUtc = null;
        state.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<int> EnsureActiveSeasonAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var season = new CosmeticSeason
        {
            Key = $"season-{Guid.NewGuid():N}",
            Name = "Test Season",
            Status = CosmeticSeasonStatuses.Active,
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        db.CosmeticSeasons.Add(season);
        await db.SaveChangesAsync();
        return season.Id;
    }

    private async Task SeedDailyRunChestClaimAsync(string userId, string transactionId, int xp)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        db.DailyRunChestClaims.Add(new DailyRunChestClaim
        {
            UserId = userId,
            Day = DateOnly.FromDateTime(DateTime.UtcNow),
            TransactionId = transactionId,
            Xp = xp,
            Coins = 10,
            CosmeticFragment = "Comet Frame Fragment",
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private async Task<(int EarnedXp, int Level)> GetSeasonProgressAsync(string userId, int seasonId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var progress = await db.UserSeasonProgresses
            .AsNoTracking()
            .FirstAsync(x => x.UserId == userId && x.SeasonId == seasonId);
        return (progress.EarnedXp, progress.Level);
    }

    private async Task<int> EnsureSeasonMilestoneAsync(int seasonId, int xpRequired, string rewardType, string payloadJson)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var entry = new SeasonRewardTrackEntry
        {
            SeasonId = seasonId,
            TrackType = CosmeticTrackTypes.Free,
            Tier = Random.Shared.Next(1000, 9999),
            XpRequired = xpRequired,
            RewardType = rewardType,
            RewardPayloadJson = payloadJson,
            IsActive = true
        };
        db.SeasonRewardTrackEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    private async Task SetSeasonXpAsync(string userId, int seasonId, int earnedXp)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var progress = await db.UserSeasonProgresses.FirstOrDefaultAsync(x => x.UserId == userId && x.SeasonId == seasonId);
        if (progress is null)
        {
            progress = new UserSeasonProgress
            {
                UserId = userId,
                SeasonId = seasonId
            };
            db.UserSeasonProgresses.Add(progress);
        }

        progress.EarnedXp = earnedXp;
        progress.Level = 1 + (earnedXp / 100);
        progress.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<int> EnsureCosmeticItemAsync(string key, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var existing = await db.CosmeticItems.FirstOrDefaultAsync(x => x.Key == key);
        if (existing is not null)
            return existing.Id;

        var item = new CosmeticItem
        {
            Key = key,
            Name = name,
            Category = CosmeticCategories.Frame,
            Rarity = "common",
            AssetPath = "/assets/test.png",
            UnlockType = CosmeticUnlockTypes.RewardRule,
            IsActive = true,
            AssetVersion = "1"
        };
        db.CosmeticItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task<int> CountOwnedItemAsync(string userId, int itemId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserCosmeticInventories.CountAsync(x => x.UserId == userId && x.CosmeticItemId == itemId && !x.IsRevoked);
    }
}

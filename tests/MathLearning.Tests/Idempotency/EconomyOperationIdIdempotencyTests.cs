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

namespace MathLearning.Tests.Idempotency;

/// <summary>
/// HTTP idempotency contract for economy mutations with explicit operationId wiring.
/// </summary>
public sealed class EconomyOperationIdIdempotencyTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string DailyGrantRuleJson = """
        {"coins":{"type":"const","value":20},"xp":{"type":"const","value":15}}
        """;
    private const string AlwaysEligibleRuleJson = """
        {"type":"always"}
        """;

    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public EconomyOperationIdIdempotencyTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CoinSpend_PersistsOperationId_RetryAndConflict()
    {
        var userId = NewUserId("coins");
        await EnsureUserAsync(userId, coins: 100);
        const string operationId = "op-coins-spend-1";
        const string idempotencyKey = "key-coins-spend-1";

        var first = await PostAsync(userId, "/api/economy/coins/spend", new
        {
            operationId,
            idempotencyKey,
            amount = 10,
            reason = "hint"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(90, await GetCoinsAsync(userId));

        var tx = await GetEconomyTransactionAsync(userId, "economy_coins_spend", idempotencyKey);
        Assert.Equal(operationId, tx.OperationId);

        var retry = await PostAsync(userId, "/api/economy/coins/spend", new
        {
            operationId,
            idempotencyKey,
            amount = 10,
            reason = "hint"
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryPayload = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(retryPayload.GetProperty("alreadyProcessed").GetBoolean());
        Assert.Equal(90, await GetCoinsAsync(userId));

        var conflict = await PostAsync(userId, "/api/economy/coins/spend", new
        {
            operationId,
            idempotencyKey,
            amount = 20,
            reason = "hint"
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("idempotency_conflict", (await conflict.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("errorCode").GetString());
        Assert.Equal(90, await GetCoinsAsync(userId));
    }

    [Fact]
    public async Task HintUse_PersistsOperationId_RetryAndConflict()
    {
        var userId = NewUserId("hint");
        await EnsureUserAsync(userId, coins: 100);
        const string operationId = "op-hint-1";
        const string idempotencyKey = "key-hint-1";

        var first = await PostAsync(userId, "/api/economy/hints/use", new
        {
            operationId,
            idempotencyKey,
            questionId = 42,
            hintType = "clue",
            costCoins = 10
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var tx = await GetEconomyTransactionAsync(userId, "economy_hint_use", idempotencyKey);
        Assert.Equal(operationId, tx.OperationId);

        var retry = await PostAsync(userId, "/api/economy/hints/use", new
        {
            operationId,
            idempotencyKey,
            questionId = 42,
            hintType = "clue",
            costCoins = 10
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.True((await retry.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("alreadyProcessed").GetBoolean());

        var conflict = await PostAsync(userId, "/api/economy/hints/use", new
        {
            operationId,
            idempotencyKey,
            questionId = 99,
            hintType = "clue",
            costCoins = 10
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("idempotency_conflict", (await conflict.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task RewardClaim_PersistsOperationId_RetryAndConflict()
    {
        var userId = NewUserId("reward");
        await EnsureUserAsync(userId, coins: 10, xp: 0);
        await EnsureRewardCatalogSeededAsync();
        const string operationId = "op-reward-1";
        const string idempotencyKey = "key-reward-1";

        var first = await PostAsync(userId, "/api/economy/rewards/claim", new
        {
            operationId,
            idempotencyKey,
            rewardId = "daily:op-id-test",
            rewardType = "daily",
            coins = 1,
            xp = 1
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var tx = await GetEconomyTransactionAsync(userId, "economy_reward_claim", idempotencyKey);
        Assert.Equal(operationId, tx.OperationId);

        var retry = await PostAsync(userId, "/api/economy/rewards/claim", new
        {
            operationId,
            idempotencyKey,
            rewardId = "daily:op-id-test",
            rewardType = "daily",
            coins = 1,
            xp = 1
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.True((await retry.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("alreadyClaimed").GetBoolean());

        var conflict = await PostAsync(userId, "/api/economy/rewards/claim", new
        {
            operationId,
            idempotencyKey,
            rewardId = "daily:op-id-test",
            rewardType = "daily",
            coins = 99,
            xp = 1
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("idempotency_conflict", (await conflict.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task StreakFreezePurchase_PersistsOperationId_RetryAndConflict()
    {
        var userId = NewUserId("freeze");
        await EnsureUserAsync(userId, coins: 200, streakFreezes: 0);
        const string operationId = "op-freeze-1";
        const string idempotencyKey = "key-freeze-1";

        var first = await PostAsync(userId, "/api/shop/streak-freeze/purchase", new
        {
            operationId,
            idempotencyKey,
            quantity = 1
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(1, await GetStreakFreezeCountAsync(userId));

        var tx = await GetEconomyTransactionAsync(userId, "shop_streak_freeze_purchase", idempotencyKey);
        Assert.Equal(operationId, tx.OperationId);

        var retry = await PostAsync(userId, "/api/shop/streak-freeze/purchase", new
        {
            operationId,
            idempotencyKey,
            quantity = 1
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.True((await retry.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("alreadyProcessed").GetBoolean());
        Assert.Equal(1, await GetStreakFreezeCountAsync(userId));

        var conflict = await PostAsync(userId, "/api/shop/streak-freeze/purchase", new
        {
            operationId,
            idempotencyKey,
            quantity = 2
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("idempotency_conflict", (await conflict.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("errorCode").GetString());
        Assert.Equal(1, await GetStreakFreezeCountAsync(userId));
    }

    [Fact]
    public async Task WithoutOperationId_FallsBackToIdempotencyKey()
    {
        var userId = NewUserId("fallback");
        await EnsureUserAsync(userId, coins: 50);
        const string idempotencyKey = "key-only-coins";

        var response = await PostAsync(userId, "/api/economy/coins/spend", new
        {
            idempotencyKey,
            amount = 5,
            reason = "other"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tx = await GetEconomyTransactionAsync(userId, "economy_coins_spend", idempotencyKey);
        Assert.Equal(idempotencyKey, tx.OperationId);
    }

    private static string NewUserId(string suffix) => $"econ-opid-{suffix}-{Guid.NewGuid():N}";

    private async Task<HttpResponseMessage> PostAsync(string userId, string url, object payload)
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

        if (!await db.Users.AnyAsync(x => x.Id == userId))
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
            profile = new UserProfile { UserId = userId, Username = userId, DisplayName = userId };
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

    private async Task<EconomyTransaction> GetEconomyTransactionAsync(
        string userId,
        string transactionType,
        string idempotencyKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.EconomyTransactions.FirstAsync(
            x => x.UserId == userId &&
                 x.TransactionType == transactionType &&
                 x.IdempotencyKey == idempotencyKey);
    }

    private async Task EnsureRewardCatalogSeededAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        if (await db.EconomyRewardDefinitions.AnyAsync())
            return;

        db.EconomyRewardDefinitions.Add(new EconomyRewardDefinition
        {
            Id = Guid.Parse("7B40D3BA-E74D-4E25-BD84-60D2D645A1C1"),
            RewardIdPattern = "^daily:(?<slug>.+)$",
            RewardType = "daily",
            Priority = 20,
            EligibilityRuleJson = AlwaysEligibleRuleJson,
            GrantRuleJson = DailyGrantRuleJson,
            IneligibilityMessage = "Reward is not eligible.",
            IsSingleUse = true,
            IsActive = true,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

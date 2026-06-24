using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class EconomySettlementEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string AlwaysEligibleRuleJson = """
        {"type":"always"}
        """;
    private const string DailyGrantRuleJson = """
        {"coins":{"type":"const","value":20},"xp":{"type":"const","value":15}}
        """;
    private const string GenericOnboardingGrantRuleJson = """
        {"coins":{"type":"const","value":50},"xp":{"type":"const","value":0}}
        """;
    private const string GenericStarterGrantRuleJson = """
        {"coins":{"type":"const","value":25},"xp":{"type":"const","value":0}}
        """;
    private const string GenericWelcomeBackGrantRuleJson = """
        {"coins":{"type":"const","value":15},"xp":{"type":"const","value":10}}
        """;
    private const string LevelEligibilityRuleJson = """
        {"type":"compare","operator":"gte","left":{"type":"profile","field":"level"},"right":{"type":"capture","name":"threshold"}}
        """;
    private const string LevelGrantRuleJson = """
        {"coins":{"type":"clamp","value":{"type":"multiply","left":{"type":"capture","name":"threshold"},"right":{"type":"const","value":10}},"min":{"type":"const","value":10}},"xp":{"type":"const","value":0}}
        """;
    private const string StreakEligibilityRuleJson = """
        {"type":"compare","operator":"gte","left":{"type":"profile","field":"streak"},"right":{"type":"capture","name":"threshold"}}
        """;
    private const string StreakGrantRuleJson = """
        {"coins":{"type":"clamp","value":{"type":"multiply","left":{"type":"capture","name":"threshold"},"right":{"type":"const","value":5}},"min":{"type":"const","value":10},"max":{"type":"const","value":500}},"xp":{"type":"const","value":0}}
        """;

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
    public async Task RewardClaim_IgnoresClientSuppliedCoinsAndXp()
    {
        var userId = $"user-reward-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 30, xp: 5);
        await EnsureRewardCatalogSeededAsync();

        var first = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-key-1",
            rewardId = "daily:lesson-complete",
            rewardType = "daily",
            coins = 999999,
            xp = 999999
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var payload = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(20, payload.GetProperty("reward").GetProperty("coins").GetInt32());
        Assert.Equal(15, payload.GetProperty("reward").GetProperty("xp").GetInt32());

        var economy = await GetEconomyAsync(userId);
        Assert.Equal(50, economy.Coins);
        Assert.Equal(20, economy.Xp);
    }

    [Fact]
    public async Task RewardClaim_UnknownReward_RejectedWithoutMutation()
    {
        var userId = $"user-reward-unknown-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 30, xp: 5);
        await EnsureRewardCatalogSeededAsync();

        var response = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-unknown-1",
            rewardId = "generic:not-in-catalog",
            rewardType = "generic",
            coins = 999999,
            xp = 999999
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unknown_reward", payload.GetProperty("errorCode").GetString());

        var economy = await GetEconomyAsync(userId);
        Assert.Equal(30, economy.Coins);
        Assert.Equal(5, economy.Xp);
    }

    [Fact]
    public async Task RewardClaim_DailyDateRewardId_IsAccepted()
    {
        var userId = $"user-reward-daily-date-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 30, xp: 5);
        await EnsureRewardCatalogSeededAsync();

        var response = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-daily-date-1",
            rewardId = "daily:2026-05-20",
            rewardType = "daily",
            coins = 999999,
            xp = 999999
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.False(payload.GetProperty("alreadyClaimed").GetBoolean());
        Assert.Equal(20, payload.GetProperty("reward").GetProperty("coins").GetInt32());
        Assert.Equal(15, payload.GetProperty("reward").GetProperty("xp").GetInt32());

        var economy = await GetEconomyAsync(userId);
        Assert.Equal(50, economy.Coins);
        Assert.Equal(20, economy.Xp);
    }

    [Fact]
    public async Task RewardClaim_DailyDateRewardId_DifferentIdempotencyKeys_DoesNotDoubleGrant()
    {
        var userId = $"user-reward-daily-date-dup-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 30, xp: 5);
        await EnsureRewardCatalogSeededAsync();

        var first = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-daily-date-dup-1",
            rewardId = "daily:2026-05-21",
            rewardType = "daily",
            coins = 1,
            xp = 1
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var duplicate = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-daily-date-dup-2",
            rewardId = "daily:2026-05-21",
            rewardType = "daily",
            coins = 999999,
            xp = 999999
        });
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

        var duplicatePayload = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(duplicatePayload.GetProperty("alreadyClaimed").GetBoolean());

        var economy = await GetEconomyAsync(userId);
        Assert.Equal(50, economy.Coins);
        Assert.Equal(20, economy.Xp);
    }

    [Fact]
    public async Task RewardClaim_DailyRewardLegacyPrefix_ReturnsInvalidRewardId()
    {
        var userId = $"user-reward-daily-legacy-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 30, xp: 5);
        await EnsureRewardCatalogSeededAsync();

        var response = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-daily-legacy-1",
            rewardId = $"daily-reward:{userId}:2026-05-20",
            rewardType = "daily",
            coins = 999999,
            xp = 999999
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errorCode = payload.GetProperty("errorCode").GetString();
        Assert.Contains(errorCode, new[] { "invalid_reward_id", "invalid_reward_type" });

        var economy = await GetEconomyAsync(userId);
        Assert.Equal(30, economy.Coins);
        Assert.Equal(5, economy.Xp);
    }

    [Fact]
    public async Task RewardClaim_SameIdempotencyKey_SamePayload_ReplaysStoredResult()
    {
        var userId = $"user-reward-retry-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 10, xp: 0);
        await EnsureRewardCatalogSeededAsync();

        var first = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-retry-1",
            rewardId = "daily:retryable",
            rewardType = "daily",
            coins = 500,
            xp = 500
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var retry = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-retry-1",
            rewardId = "daily:retryable",
            rewardType = "daily",
            coins = 500,
            xp = 500
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        var retryPayload = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(retryPayload.GetProperty("alreadyClaimed").GetBoolean());

        var economy = await GetEconomyAsync(userId);
        Assert.Equal(30, economy.Coins);
        Assert.Equal(15, economy.Xp);
    }

    [Fact]
    public async Task RewardClaim_SameIdempotencyKey_DifferentPayload_ReturnsConflict()
    {
        var userId = $"user-reward-conflict-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 10, xp: 0);
        await EnsureRewardCatalogSeededAsync();

        var first = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-conflict-1",
            rewardId = "daily:conflict",
            rewardType = "daily",
            coins = 10,
            xp = 10
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var conflict = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-conflict-1",
            rewardId = "daily:conflict",
            rewardType = "daily",
            coins = 20,
            xp = 10
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var payload = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("idempotency_conflict", payload.GetProperty("errorCode").GetString());

        var economy = await GetEconomyAsync(userId);
        Assert.Equal(30, economy.Coins);
        Assert.Equal(15, economy.Xp);
    }

    [Fact]
    public async Task RewardClaim_LevelReward_SameIdempotencyKey_DifferentPayload_ReturnsConflict()
    {
        var userId = $"user-reward-level-conflict-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 5, xp: 600);
        await EnsureRewardCatalogSeededAsync();

        var first = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-level-conflict-1",
            rewardId = "level:2",
            rewardType = "level",
            coins = 999,
            xp = 999
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var conflict = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-level-conflict-1",
            rewardId = "level:7",
            rewardType = "level",
            coins = 999,
            xp = 999
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var payload = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("idempotency_conflict", payload.GetProperty("errorCode").GetString());

        var economy = await GetEconomyAsync(userId);
        Assert.Equal(25, economy.Coins);
        Assert.Equal(600, economy.Xp);
    }

    [Fact]
    public async Task RewardClaim_SameRewardId_DifferentIdempotencyKeys_DoesNotDoubleGrant()
    {
        var userId = $"user-reward-duplicate-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 30, xp: 0);
        await EnsureRewardCatalogSeededAsync();

        var first = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-dup-1",
            rewardId = "daily:duplicate-guard",
            rewardType = "daily",
            coins = 1,
            xp = 1
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var duplicateRewardId = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-dup-2",
            rewardId = "daily:duplicate-guard",
            rewardType = "daily",
            coins = 999999,
            xp = 999999
        });
        Assert.Equal(HttpStatusCode.OK, duplicateRewardId.StatusCode);
        var dupPayload = await duplicateRewardId.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(dupPayload.GetProperty("alreadyClaimed").GetBoolean());

        var economy = await GetEconomyAsync(userId);
        Assert.Equal(50, economy.Coins);
        Assert.Equal(15, economy.Xp);
    }

    [Fact]
    public async Task RewardClaim_LevelReward_DifferentIdempotencyKeys_DoesNotDoubleGrant()
    {
        var userId = $"user-reward-level-duplicate-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 5, xp: 600);
        await EnsureRewardCatalogSeededAsync();

        var first = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-level-dup-1",
            rewardId = "level:7",
            rewardType = "level",
            coins = 999,
            xp = 999
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var duplicate = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-level-dup-2",
            rewardId = "level:7",
            rewardType = "level",
            coins = 999,
            xp = 999
        });
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

        var duplicatePayload = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(duplicatePayload.GetProperty("alreadyClaimed").GetBoolean());

        var economy = await GetEconomyAsync(userId);
        Assert.Equal(75, economy.Coins);
        Assert.Equal(600, economy.Xp);
    }

    [Fact]
    public async Task RewardClaim_LevelThreshold_UsesDataDrivenCatalogRules()
    {
        var userId = $"user-reward-level-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 5, xp: 600);
        await EnsureRewardCatalogSeededAsync();

        var response = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-level-1",
            rewardId = "level:7",
            rewardType = "level",
            coins = 999,
            xp = 999
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.False(payload.GetProperty("alreadyClaimed").GetBoolean());
        Assert.Equal(75, payload.GetProperty("coins").GetInt32());
        Assert.Equal(600, payload.GetProperty("xp").GetInt32());
        Assert.Equal(70, payload.GetProperty("reward").GetProperty("coins").GetInt32());
        Assert.Equal(0, payload.GetProperty("reward").GetProperty("xp").GetInt32());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("errorCode").ValueKind);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("message").ValueKind);

        var economy = await GetEconomyAsync(userId);
        Assert.Equal(75, economy.Coins);
        Assert.Equal(600, economy.Xp);
    }

    [Theory]
    [InlineData("level:0")]
    [InlineData("level:01")]
    [InlineData("level:not-a-number")]
    [InlineData("level:214748365")]
    public async Task RewardClaim_InvalidLevelRewardId_ReturnsInvalidRewardId(string rewardId)
    {
        var userId = $"user-reward-level-invalid-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 5, xp: 600);
        await EnsureRewardCatalogSeededAsync();

        var response = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = $"reward-level-invalid-{Guid.NewGuid():N}",
            rewardId,
            rewardType = "level",
            coins = 999,
            xp = 999
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_reward_id", payload.GetProperty("errorCode").GetString());

        var economy = await GetEconomyAsync(userId);
        Assert.Equal(5, economy.Coins);
        Assert.Equal(600, economy.Xp);
    }

    [Fact]
    public async Task RewardClaim_KnownRewardBlockedByEligibilityState_DoesNotMutate()
    {
        var userId = $"user-reward-blocked-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 30, xp: 0);
        await EnsureRewardCatalogSeededAsync();

        await SetRewardEligibilityAsync(userId, "generic:starter_bonus", eligible: false);
        var before = await GetEconomyAsync(userId);

        var blocked = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = "reward-blocked-1",
            rewardId = "generic:starter_bonus",
            rewardType = "generic",
            coins = 999,
            xp = 0
        });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var blockedPayload = await blocked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_eligible", blockedPayload.GetProperty("errorCode").GetString());

        var after = await GetEconomyAsync(userId);
        Assert.Equal(before.Coins, after.Coins);
        Assert.Equal(before.Xp, after.Xp);
    }

    [Fact]
    public async Task RewardPreview_DoesNotMutateBalancesInventoryOrTransactions()
    {
        var userId = $"user-reward-preview-stable-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 5, xp: 600);
        await EnsureRewardCatalogSeededAsync();
        var itemId = await EnsureCosmeticItemAsync($"preview-item-{Guid.NewGuid():N}", "Preview Item");

        var beforeEconomy = await GetEconomyAsync(userId);
        var beforeInventory = await CountOwnedItemAsync(userId, itemId);
        var beforeTransactions = await CountEconomyTransactionsAsync(userId);

        var response = await GetAsUserAsync(userId, "/api/economy/rewards/preview?rewardId=level:7&rewardType=level");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var preview = payload.GetProperty("preview");
        Assert.True(preview.GetProperty("isEligible").GetBoolean());
        Assert.Equal(70, preview.GetProperty("displayCoins").GetInt32());
        Assert.Equal(0, preview.GetProperty("displayXp").GetInt32());
        Assert.Equal(JsonValueKind.Null, preview.GetProperty("cosmetic").ValueKind);
        Assert.Equal(JsonValueKind.Null, preview.GetProperty("fragment").ValueKind);

        var afterEconomy = await GetEconomyAsync(userId);
        var afterInventory = await CountOwnedItemAsync(userId, itemId);
        var afterTransactions = await CountEconomyTransactionsAsync(userId);

        Assert.Equal(beforeEconomy.Coins, afterEconomy.Coins);
        Assert.Equal(beforeEconomy.Xp, afterEconomy.Xp);
        Assert.Equal(beforeInventory, afterInventory);
        Assert.Equal(beforeTransactions, afterTransactions);
    }

    [Fact]
    public async Task RewardPreview_GenericReward_DoesNotCreateClaimState_AndClaimMatchesPreview()
    {
        var userId = $"user-reward-preview-generic-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 10, xp: 0);
        await EnsureRewardCatalogSeededAsync();

        const string rewardId = "generic:onboarding_bonus";
        var beforeEconomy = await GetEconomyAsync(userId);
        var beforeTransactions = await CountEconomyTransactionsAsync(userId);
        var beforeRewardState = await CountRewardStateEntriesAsync(userId, rewardId);

        var previewResponse = await GetAsUserAsync(
            userId,
            "/api/economy/rewards/preview?rewardId=generic:onboarding_bonus&rewardType=generic");

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var previewPayload = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var preview = previewPayload.GetProperty("preview");
        Assert.True(preview.GetProperty("isEligible").GetBoolean());
        var previewCoins = preview.GetProperty("displayCoins").GetInt32();
        var previewXp = preview.GetProperty("displayXp").GetInt32();
        Assert.Equal(50, previewCoins);
        Assert.Equal(0, previewXp);

        var afterPreviewEconomy = await GetEconomyAsync(userId);
        var afterPreviewTransactions = await CountEconomyTransactionsAsync(userId);
        var afterPreviewRewardState = await CountRewardStateEntriesAsync(userId, rewardId);

        Assert.Equal(beforeEconomy.Coins, afterPreviewEconomy.Coins);
        Assert.Equal(beforeEconomy.Xp, afterPreviewEconomy.Xp);
        Assert.Equal(beforeTransactions, afterPreviewTransactions);
        Assert.Equal(beforeRewardState, afterPreviewRewardState);

        var claimResponse = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = $"reward-preview-generic-{Guid.NewGuid():N}",
            rewardId = rewardId,
            rewardType = "generic",
            coins = 999,
            xp = 999
        });

        Assert.Equal(HttpStatusCode.OK, claimResponse.StatusCode);
        var claimPayload = await claimResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(claimPayload.GetProperty("alreadyClaimed").GetBoolean());
        Assert.Equal(previewCoins, claimPayload.GetProperty("reward").GetProperty("coins").GetInt32());
        Assert.Equal(previewXp, claimPayload.GetProperty("reward").GetProperty("xp").GetInt32());
    }

    [Fact]
    public async Task RewardPreview_LevelReward_MatchesClaimRewardCalculation()
    {
        var userId = $"user-reward-preview-level-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 5, xp: 600);
        await EnsureRewardCatalogSeededAsync();

        var previewResponse = await GetAsUserAsync(userId, "/api/economy/rewards/preview?rewardId=level:7&rewardType=level");
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);

        var previewPayload = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var preview = previewPayload.GetProperty("preview");
        var previewCoins = preview.GetProperty("displayCoins").GetInt32();
        var previewXp = preview.GetProperty("displayXp").GetInt32();

        var claimResponse = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = $"reward-preview-level-{Guid.NewGuid():N}",
            rewardId = "level:7",
            rewardType = "level",
            coins = 999,
            xp = 999
        });

        Assert.Equal(HttpStatusCode.OK, claimResponse.StatusCode);
        var claimPayload = await claimResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(previewCoins, claimPayload.GetProperty("reward").GetProperty("coins").GetInt32());
        Assert.Equal(previewXp, claimPayload.GetProperty("reward").GetProperty("xp").GetInt32());
    }

    [Fact]
    public async Task RewardPreview_DailyRun_UsesServerCanonicalReward()
    {
        var userId = $"user-reward-preview-daily-{Guid.NewGuid():N}";
        var day = new DateOnly(2026, 05, 20);
        const string dayText = "2026-05-20";
        const string transactionId = "daily-preview-tx-1";

        await EnsureUserAsync(userId, coins: 30, xp: 40);
        await SetDailyRunCompletionAsync(userId, day, completed: true);

        var beforeEconomy = await GetEconomyAsync(userId);
        var previewResponse = await GetAsUserAsync(
            userId,
            $"/api/economy/rewards/preview?rewardId=daily-run:{dayText}&rewardType=daily&transactionId={transactionId}&date={dayText}");

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var previewPayload = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var preview = previewPayload.GetProperty("preview");
        Assert.True(preview.GetProperty("isEligible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, preview.GetProperty("cosmetic").ValueKind);

        var previewCoins = preview.GetProperty("displayCoins").GetInt32();
        var previewXp = preview.GetProperty("displayXp").GetInt32();
        var previewFragment = preview.GetProperty("fragment");

        var afterPreviewEconomy = await GetEconomyAsync(userId);
        Assert.Equal(beforeEconomy.Coins, afterPreviewEconomy.Coins);
        Assert.Equal(beforeEconomy.Xp, afterPreviewEconomy.Xp);

        var claimResponse = await PostAsUserAsync(userId, "/api/daily-run/chest/claim", new
        {
            transactionId,
            date = dayText
        });

        Assert.Equal(HttpStatusCode.OK, claimResponse.StatusCode);
        var claimPayload = await claimResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(previewCoins, claimPayload.GetProperty("reward").GetProperty("coins").GetInt32());
        Assert.Equal(previewXp, claimPayload.GetProperty("reward").GetProperty("xp").GetInt32());
        Assert.Equal(
            previewFragment.GetProperty("name").GetString(),
            claimPayload.GetProperty("reward").GetProperty("cosmeticFragment").GetString());
        Assert.Equal(
            previewFragment.GetProperty("copies").GetInt32(),
            claimPayload.GetProperty("reward").GetProperty("fragmentCopies").GetInt32());
    }

    [Fact]
    public async Task RewardPreview_DoesNotAlterSeasonOrProgressState()
    {
        var userId = $"user-reward-preview-season-state-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 5, xp: 600);
        await EnsureRewardCatalogSeededAsync();

        var seasonId = await EnsureActiveSeasonAsync();
        var milestoneId = await EnsureSeasonMilestoneAsync(seasonId, xpRequired: 50, rewardType: "coins", payloadJson: """{"coins":25}""");
        await SetSeasonXpAsync(userId, seasonId, earnedXp: 120);

        var beforeProgress = await GetSeasonProgressAsync(userId, seasonId);
        var beforeMilestoneClaims = await CountSeasonMilestoneClaimsAsync(userId, seasonId);
        var beforeSeasonDailyRunClaims = await CountSeasonDailyRunClaimsAsync(userId, seasonId);

        var previewResponse = await GetAsUserAsync(
            userId,
            $"/api/economy/rewards/preview?rewardId=level:7&rewardType=level&seasonId={seasonId}&milestoneId={milestoneId}");

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);

        var afterProgress = await GetSeasonProgressAsync(userId, seasonId);
        var afterMilestoneClaims = await CountSeasonMilestoneClaimsAsync(userId, seasonId);
        var afterSeasonDailyRunClaims = await CountSeasonDailyRunClaimsAsync(userId, seasonId);

        Assert.Equal(beforeProgress.EarnedXp, afterProgress.EarnedXp);
        Assert.Equal(beforeProgress.Level, afterProgress.Level);
        Assert.Equal(beforeMilestoneClaims, afterMilestoneClaims);
        Assert.Equal(beforeSeasonDailyRunClaims, afterSeasonDailyRunClaims);
    }

    [Fact]
    public async Task RewardPreview_AlreadyClaimedReward_ReturnsIneligible()
    {
        var userId = $"user-reward-preview-claimed-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 10, xp: 0);
        await EnsureRewardCatalogSeededAsync();

        var claim = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = $"reward-preview-claimed-{Guid.NewGuid():N}",
            rewardId = "generic:starter_bonus",
            rewardType = "generic",
            coins = 0,
            xp = 0
        });
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);

        var previewResponse = await GetAsUserAsync(
            userId,
            "/api/economy/rewards/preview?rewardId=generic:starter_bonus&rewardType=generic");

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var payload = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var preview = payload.GetProperty("preview");
        Assert.False(preview.GetProperty("isEligible").GetBoolean());
        Assert.Equal("already_claimed", preview.GetProperty("reason").GetString());
        Assert.Equal(0, preview.GetProperty("displayCoins").GetInt32());
        Assert.Equal(0, preview.GetProperty("displayXp").GetInt32());
    }

    [Theory]
    [InlineData("/api/economy/rewards/preview?rewardId=level:not-a-number&rewardType=level", "invalid_reward_id")]
    [InlineData("/api/economy/rewards/preview?rewardId=level:7&rewardType=banana", "invalid_reward_type")]
    public async Task RewardPreview_InvalidReward_ReturnsStructuredReason(string url, string reason)
    {
        var userId = $"user-reward-preview-invalid-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 5, xp: 600);
        await EnsureRewardCatalogSeededAsync();

        var response = await GetAsUserAsync(userId, url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var preview = payload.GetProperty("preview");
        Assert.False(preview.GetProperty("isEligible").GetBoolean());
        Assert.Equal(reason, preview.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task RewardPreview_IneligibleReward_AndClaim_AgreeOnNotEligible()
    {
        var userId = $"user-reward-preview-ineligible-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 30, xp: 0);
        await EnsureRewardCatalogSeededAsync();

        await SetRewardEligibilityAsync(userId, "generic:starter_bonus", eligible: false);
        var beforeEconomy = await GetEconomyAsync(userId);

        var previewResponse = await GetAsUserAsync(
            userId,
            "/api/economy/rewards/preview?rewardId=generic:starter_bonus&rewardType=generic");

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var previewPayload = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var preview = previewPayload.GetProperty("preview");
        Assert.False(preview.GetProperty("isEligible").GetBoolean());
        Assert.Equal("not_eligible", preview.GetProperty("reason").GetString());

        var claimResponse = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = $"reward-preview-ineligible-{Guid.NewGuid():N}",
            rewardId = "generic:starter_bonus",
            rewardType = "generic",
            coins = 999,
            xp = 999
        });

        Assert.Equal(HttpStatusCode.Conflict, claimResponse.StatusCode);
        var claimPayload = await claimResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_eligible", claimPayload.GetProperty("errorCode").GetString());

        var afterEconomy = await GetEconomyAsync(userId);
        Assert.Equal(beforeEconomy.Coins, afterEconomy.Coins);
        Assert.Equal(beforeEconomy.Xp, afterEconomy.Xp);
    }

    [Theory]
    [InlineData("level:not-a-number", "level", "invalid_reward_id")]
    [InlineData("level:7", "banana", "invalid_reward_type")]
    public async Task RewardPreview_InvalidReward_AndClaim_AgreeOnReason(
        string rewardId,
        string rewardType,
        string expectedReason)
    {
        var userId = $"user-reward-preview-invalid-parity-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 5, xp: 600);
        await EnsureRewardCatalogSeededAsync();

        var previewResponse = await GetAsUserAsync(
            userId,
            $"/api/economy/rewards/preview?rewardId={Uri.EscapeDataString(rewardId)}&rewardType={Uri.EscapeDataString(rewardType)}");

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var previewPayload = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var preview = previewPayload.GetProperty("preview");
        Assert.False(preview.GetProperty("isEligible").GetBoolean());
        Assert.Equal(expectedReason, preview.GetProperty("reason").GetString());

        var claimResponse = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = $"reward-preview-invalid-parity-{Guid.NewGuid():N}",
            rewardId,
            rewardType,
            coins = 999,
            xp = 999
        });

        Assert.Equal(HttpStatusCode.BadRequest, claimResponse.StatusCode);
        var claimPayload = await claimResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedReason, claimPayload.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task RewardClaim_RemainsAuthoritative_AfterPreview()
    {
        var userId = $"user-reward-preview-authority-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 5, xp: 600);
        await EnsureRewardCatalogSeededAsync();

        var previewResponse = await GetAsUserAsync(userId, "/api/economy/rewards/preview?rewardId=level:7&rewardType=level");
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var previewPayload = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(previewPayload.GetProperty("preview").GetProperty("isEligible").GetBoolean());

        await MarkRewardClaimedAsync(userId, "level:7");

        var claimResponse = await PostAsUserAsync(userId, "/api/economy/rewards/claim", new
        {
            idempotencyKey = $"reward-preview-authority-{Guid.NewGuid():N}",
            rewardId = "level:7",
            rewardType = "level",
            coins = 999,
            xp = 999
        });

        Assert.Equal(HttpStatusCode.OK, claimResponse.StatusCode);
        var claimPayload = await claimResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(claimPayload.GetProperty("alreadyClaimed").GetBoolean());
    }

    [Fact]
    public async Task AdminRewardGrant_RequiresAdminRole()
    {
        var userId = $"user-admin-target-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 10, xp: 0);

        var response = await PostAsUserAsync("non-admin-user", "/api/admin/economy/rewards/grant", new
        {
            idempotencyKey = "admin-grant-unauthorized",
            userId,
            grantId = "grant-1",
            coins = 50,
            xp = 5,
            reason = "manual_test"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminRewardGrant_Succeeds_AndDoesNotDoubleGrant()
    {
        var userId = $"user-admin-target-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 10, xp: 0);

        var first = await PostAsUserAsync("admin-actor", "/api/admin/economy/rewards/grant", new
        {
            idempotencyKey = "admin-grant-1",
            userId,
            grantId = "manual-bonus-1",
            coins = 40,
            xp = 5,
            reason = "manual_test"
        }, roles: DesignTokenSecurity.AdminRole);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var retry = await PostAsUserAsync("admin-actor", "/api/admin/economy/rewards/grant", new
        {
            idempotencyKey = "admin-grant-1",
            userId,
            grantId = "manual-bonus-1",
            coins = 40,
            xp = 5,
            reason = "manual_test"
        }, roles: DesignTokenSecurity.AdminRole);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryPayload = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(retryPayload.GetProperty("alreadyProcessed").GetBoolean());

        var duplicateGrantId = await PostAsUserAsync("admin-actor", "/api/admin/economy/rewards/grant", new
        {
            idempotencyKey = "admin-grant-2",
            userId,
            grantId = "manual-bonus-1",
            coins = 999,
            xp = 999,
            reason = "manual_test"
        }, roles: DesignTokenSecurity.AdminRole);
        Assert.Equal(HttpStatusCode.OK, duplicateGrantId.StatusCode);
        var duplicatePayload = await duplicateGrantId.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(duplicatePayload.GetProperty("alreadyProcessed").GetBoolean());

        var economy = await GetEconomyAsync(userId);
        Assert.Equal(50, economy.Coins);
        Assert.Equal(5, economy.Xp);

        var auditGrant = await GetAdminRewardGrantAsync(userId, "manual-bonus-1");
        Assert.NotNull(auditGrant);
        Assert.Equal("admin-actor", auditGrant!.ActorUserId);
        Assert.Equal(40, auditGrant.Coins);
        Assert.Equal(5, auditGrant.Xp);
        Assert.Equal("manual_test", auditGrant.Reason);
        Assert.Equal(1, await CountAdminRewardGrantsAsync(userId, "manual-bonus-1"));
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

        var retry = await PostAsUserAsync(userId, $"/api/seasons/milestones/{milestoneId}/claim", new
        {
            idempotencyKey = "ms-key-2",
            seasonId
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryPayload = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(retryPayload.GetProperty("alreadyClaimed").GetBoolean());
        Assert.Equal(50, await GetCoinsAsync(userId));

        var duplicate = await PostAsUserAsync(userId, $"/api/seasons/milestones/{milestoneId}/claim", new
        {
            idempotencyKey = "ms-key-3",
            seasonId
        });
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var dupPayload = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(dupPayload.GetProperty("alreadyClaimed").GetBoolean());
        Assert.Equal(50, await GetCoinsAsync(userId));
    }

    [Fact]
    public async Task SeasonMilestone_MissingIdempotencyKey_IsRejected()
    {
        var userId = $"user-season-ms-missing-key-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 0);
        var seasonId = await EnsureActiveSeasonAsync();
        var milestoneId = await EnsureSeasonMilestoneAsync(seasonId, xpRequired: 50, rewardType: "coins", payloadJson: """{"coins":50}""");
        await SetSeasonXpAsync(userId, seasonId, earnedXp: 60);

        var response = await PostAsUserAsync(userId, $"/api/seasons/milestones/{milestoneId}/claim", new
        {
            seasonId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_idempotency_key", payload.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task CosmeticItemClaim_StringItemKey_AndAlreadyOwned_AreSafe()
    {
        var userId = $"user-item-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 100);
        var cometItemId = await EnsureCosmeticItemAsync("frame_comet", "Comet Frame");
        await EnsureCosmeticItemAsync("effect_nova_trail", "Nova Trail");

        var first = await PostAsUserAsync(userId, "/api/cosmetics/items/frame_comet/claim", new
        {
            operationId = "item-key-1",
            idempotencyKey = "item-key-1",
            source = "reward"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstPayload = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(firstPayload.GetProperty("success").GetBoolean());
        Assert.False(firstPayload.GetProperty("alreadyClaimed").GetBoolean());

        var second = await PostAsUserAsync(userId, "/api/cosmetics/items/frame_comet/claim", new
        {
            operationId = "item-key-2",
            idempotencyKey = "item-key-2",
            source = "reward"
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondPayload = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(secondPayload.GetProperty("success").GetBoolean());
        Assert.True(secondPayload.GetProperty("alreadyClaimed").GetBoolean());

        var third = await PostAsUserAsync(userId, "/api/cosmetics/items/effect_nova_trail/claim", new
        {
            operationId = "item-key-3",
            idempotencyKey = "item-key-3",
            source = "reward"
        });
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        var thirdPayload = await third.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(thirdPayload.GetProperty("success").GetBoolean());
        Assert.False(thirdPayload.GetProperty("alreadyClaimed").GetBoolean());

        var invalid = await PostAsUserAsync(userId, "/api/cosmetics/items/invalid_key/claim", new
        {
            operationId = "item-key-invalid",
            idempotencyKey = "item-key-invalid",
            source = "reward"
        });
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        var invalidPayload = await invalid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_item", invalidPayload.GetProperty("errorCode").GetString());

        var retry = await PostAsUserAsync(userId, "/api/cosmetics/items/frame_comet/claim", new
        {
            operationId = "item-key-retry",
            idempotencyKey = "item-key-retry",
            source = "reward"
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryAgain = await PostAsUserAsync(userId, "/api/cosmetics/items/frame_comet/claim", new
        {
            operationId = "item-key-retry",
            idempotencyKey = "item-key-retry",
            source = "reward"
        });
        Assert.Equal(HttpStatusCode.OK, retryAgain.StatusCode);
        Assert.Equal(1, await CountOwnedItemAsync(userId, cometItemId));
    }

    [Fact]
    public async Task CosmeticFragmentGrant_RetryNoDuplicate_UnlocksOnce()
    {
        var userId = $"user-fragment-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 100);
        var unlockItemId = await EnsureCosmeticItemAsync("frame_comet", "Comet Frame");

        var grant = await PostAsUserAsync(userId, "/api/cosmetics/fragments/grant", new
        {
            operationId = "frag-key-1",
            idempotencyKey = "frag-key-1",
            fragmentName = "Comet Frame Fragment",
            copies = 5,
            source = "reward"
        });
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
        var grantPayload = await grant.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("frame_comet", grantPayload.GetProperty("progress").GetProperty("itemId").GetString());
        Assert.Equal(5, grantPayload.GetProperty("progress").GetProperty("collectedFragments").GetInt32());
        Assert.Equal(5, grantPayload.GetProperty("progress").GetProperty("requiredFragments").GetInt32());
        Assert.True(grantPayload.GetProperty("itemUnlocked").GetBoolean());

        var retry = await PostAsUserAsync(userId, "/api/cosmetics/fragments/grant", new
        {
            operationId = "frag-key-1",
            idempotencyKey = "frag-key-1",
            fragmentName = "Comet Frame Fragment",
            copies = 5,
            source = "reward"
        });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryPayload = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(retryPayload.GetProperty("alreadyProcessed").GetBoolean());

        var secondGrant = await PostAsUserAsync(userId, "/api/cosmetics/fragments/grant", new
        {
            operationId = "frag-key-2",
            idempotencyKey = "frag-key-2",
            fragmentName = "Comet Frame Fragment",
            copies = 1,
            source = "reward"
        });
        Assert.Equal(HttpStatusCode.OK, secondGrant.StatusCode);

        var ownedCount = await CountOwnedItemAsync(userId, unlockItemId);
        Assert.Equal(1, ownedCount);
    }

    private async Task<HttpResponseMessage> PostAsUserAsync(string userId, string url, object payload, string? roles = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);
        if (!string.IsNullOrWhiteSpace(roles))
        {
            request.Headers.Add("X-Test-Roles", roles);
        }
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetAsUserAsync(string userId, string url, string? roles = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Test-UserId", userId);
        if (!string.IsNullOrWhiteSpace(roles))
        {
            request.Headers.Add("X-Test-Roles", roles);
        }
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

    private async Task<(int Coins, int Xp)> GetEconomyAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserProfiles
            .Where(x => x.UserId == userId)
            .Select(x => new ValueTuple<int, int>(x.Coins, x.Xp))
            .FirstAsync();
    }

    private async Task<int> CountEconomyTransactionsAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.EconomyTransactions.CountAsync(x => x.UserId == userId);
    }

    private async Task<int> GetStreakFreezeCountAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserProfiles.Where(x => x.UserId == userId).Select(x => x.StreakFreezeCount).FirstAsync();
    }

    private async Task<int> CountRewardStateEntriesAsync(string userId, string rewardId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserRewardStates.CountAsync(x => x.UserId == userId && x.RewardKey == rewardId);
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

    private async Task MarkRewardClaimedAsync(string userId, string rewardId)
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

        state.Eligible = true;
        state.Claimed = true;
        state.ClaimedAtUtc = DateTime.UtcNow;
        state.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task SetDailyRunCompletionAsync(string userId, DateOnly day, bool completed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var stat = await db.UserDailyStats.FirstOrDefaultAsync(x => x.UserId == userId && x.Day == day);
        if (stat is null)
        {
            stat = new UserDailyStat
            {
                UserId = userId,
                Day = day,
                Completed = completed
            };
            db.UserDailyStats.Add(stat);
        }
        else
        {
            stat.Completed = completed;
        }

        await db.SaveChangesAsync();
    }

    private async Task EnsureRewardCatalogSeededAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        if (await db.EconomyRewardDefinitions.AnyAsync())
            return;

        db.EconomyRewardDefinitions.AddRange(
            new EconomyRewardDefinition
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
            },
            new EconomyRewardDefinition
            {
                Id = Guid.Parse("2E3D6E31-3F8D-4D60-9266-3CBDB3A34729"),
                RewardIdPattern = "^generic:onboarding_bonus$",
                RewardType = "generic",
                Priority = 10,
                EligibilityRuleJson = AlwaysEligibleRuleJson,
                GrantRuleJson = GenericOnboardingGrantRuleJson,
                IneligibilityMessage = "Reward is not eligible.",
                IsSingleUse = true,
                IsActive = true,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new EconomyRewardDefinition
            {
                Id = Guid.Parse("D4E88C31-56C0-494B-9611-271DB4F1DCD8"),
                RewardIdPattern = "^generic:starter_bonus$",
                RewardType = "generic",
                Priority = 10,
                EligibilityRuleJson = AlwaysEligibleRuleJson,
                GrantRuleJson = GenericStarterGrantRuleJson,
                IneligibilityMessage = "Reward is not eligible.",
                IsSingleUse = true,
                IsActive = true,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new EconomyRewardDefinition
            {
                Id = Guid.Parse("E1F90A77-EEB8-4FD7-973E-E05449B7678A"),
                RewardIdPattern = "^generic:welcome_back$",
                RewardType = "generic",
                Priority = 10,
                EligibilityRuleJson = AlwaysEligibleRuleJson,
                GrantRuleJson = GenericWelcomeBackGrantRuleJson,
                IneligibilityMessage = "Reward is not eligible.",
                IsSingleUse = true,
                IsActive = true,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new EconomyRewardDefinition
            {
                Id = Guid.Parse("D9D5E0D8-87FA-4819-BE4A-6285C2EF6FC7"),
                RewardIdPattern = "^level:(?<threshold>[1-9]\\d*)$",
                RewardType = "level",
                Priority = 30,
                EligibilityRuleJson = LevelEligibilityRuleJson,
                GrantRuleJson = LevelGrantRuleJson,
                IneligibilityMessage = "Reward is not eligible.",
                IsSingleUse = true,
                IsActive = true,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new EconomyRewardDefinition
            {
                Id = Guid.Parse("FA5D14D5-7931-4B57-B5D0-442AFC4BA26E"),
                RewardIdPattern = "^streak:(?<threshold>[1-9]\\d*)$",
                RewardType = "streak",
                Priority = 30,
                EligibilityRuleJson = StreakEligibilityRuleJson,
                GrantRuleJson = StreakGrantRuleJson,
                IneligibilityMessage = "Reward is not eligible.",
                IsSingleUse = true,
                IsActive = true,
                UpdatedAtUtc = DateTime.UtcNow
            });

        await db.SaveChangesAsync();
    }

    private async Task<AdminEconomyRewardGrant?> GetAdminRewardGrantAsync(string userId, string grantId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        return await db.AdminEconomyRewardGrants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.GrantId == grantId);
    }

    private async Task<int> CountAdminRewardGrantsAsync(string userId, string grantId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        return await db.AdminEconomyRewardGrants
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && x.GrantId == grantId);
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

    private async Task SeedDailyRunChestClaimAsync(
        string userId,
        string transactionId,
        int xp,
        string fragmentName = "Comet Frame Fragment",
        int fragmentCopies = 2)
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
            CosmeticFragment = fragmentName,
            FragmentCopies = fragmentCopies,
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

    private async Task<int> CountSeasonMilestoneClaimsAsync(string userId, int seasonId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserSeasonMilestoneClaims.CountAsync(x => x.UserId == userId && x.SeasonId == seasonId);
    }

    private async Task<int> CountSeasonDailyRunClaimsAsync(string userId, int seasonId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserSeasonDailyRunClaims.CountAsync(x => x.UserId == userId && x.SeasonId == seasonId);
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

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

namespace MathLearning.Tests.Contracts;

public sealed class MobileEconomyContractIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
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

    public MobileEconomyContractIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RewardClaim_DailyFlutterPayload_SettlesAndIdempotencyRulesApply()
    {
        var userId = $"mobile-contract-daily-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 20, xp: 0);
        await EnsureRewardCatalogSeededAsync();

        const string dayKey = "2026-05-20";
        var idempotencyKey = $"reward:daily:{userId}:{dayKey}";

        var first = await PostAsUserAsync(
            userId,
            "/api/economy/rewards/claim",
            MobileEconomyContractPayloads.RewardClaimDaily(idempotencyKey, dayKey));

        Assert.NotEqual(HttpStatusCode.NotFound, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstPayload = await ReadJsonAsync(first);
        Assert.True(firstPayload.GetProperty("success").GetBoolean());
        Assert.Equal(20, firstPayload.GetProperty("reward").GetProperty("coins").GetInt32());
        Assert.Equal(15, firstPayload.GetProperty("reward").GetProperty("xp").GetInt32());

        var replay = await PostAsUserAsync(
            userId,
            "/api/economy/rewards/claim",
            MobileEconomyContractPayloads.RewardClaimDaily(idempotencyKey, dayKey));

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayPayload = await ReadJsonAsync(replay);
        Assert.True(replayPayload.GetProperty("alreadyClaimed").GetBoolean());

        var conflict = await PostAsUserAsync(
            userId,
            "/api/economy/rewards/claim",
            MobileEconomyContractPayloads.RewardClaimLevel(idempotencyKey, level: 2));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await AssertBusinessErrorCodeAsync(conflict, "idempotency_conflict");
    }

    [Fact]
    public async Task RewardClaim_LevelFlutterPayload_UsesCatalogAndAvoidsShapeFailures()
    {
        var userId = $"mobile-contract-level-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 5, xp: 600);
        await EnsureRewardCatalogSeededAsync();

        var response = await PostAsUserAsync(
            userId,
            "/api/economy/rewards/claim",
            MobileEconomyContractPayloads.RewardClaimLevel(
                idempotencyKey: $"reward:level:{userId}:7",
                level: 7));

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.Equal(70, payload.GetProperty("reward").GetProperty("coins").GetInt32());
        Assert.Equal(0, payload.GetProperty("reward").GetProperty("xp").GetInt32());
    }

    [Fact]
    public async Task CosmeticItemClaim_FlutterPayload_UsesStringRouteAndBusinessErrors()
    {
        var userId = $"mobile-contract-item-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 0, xp: 0);
        var itemId = await EnsureCosmeticItemAsync("frame_comet", "Comet Frame");
        var entitlement = await CosmeticEntitlementTestSeeder.SeedItemEntitlementAsync(
            _factory.Services,
            userId,
            itemId,
            "reward_track",
            "season:7:tier:3");
        var secondEntitlement = await CosmeticEntitlementTestSeeder.SeedItemEntitlementAsync(
            _factory.Services,
            userId,
            itemId,
            "reward_track",
            "season:7:tier:4");

        var firstPayload = MobileEconomyContractPayloads.CosmeticItemClaim(
            idempotencyKey: "cosmetic_claim/user-a/reward/level:7/frame_comet",
            sourceType: "reward",
            entitlementId: entitlement.Id,
            sourceEvent: "level:7");

        var first = await PostAsUserAsync(
            userId,
            "/api/cosmetics/items/frame_comet/claim",
            firstPayload);

        Assert.NotEqual(HttpStatusCode.NotFound, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResponse = await ReadJsonAsync(first);
        Assert.True(firstResponse.GetProperty("success").GetBoolean());

        var replay = await PostAsUserAsync(
            userId,
            "/api/cosmetics/items/frame_comet/claim",
            firstPayload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);

        var conflictPayload = MobileEconomyContractPayloads.CosmeticItemClaim(
            idempotencyKey: "cosmetic_claim/user-a/reward/level:7/frame_comet",
            sourceType: "season",
            entitlementId: secondEntitlement.Id,
            sourceEvent: "milestone:2");

        var conflict = await PostAsUserAsync(
            userId,
            "/api/cosmetics/items/frame_comet/claim",
            conflictPayload);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var conflictResponse = await ReadJsonAsync(conflict);
        Assert.Equal("idempotency_conflict", conflictResponse.GetProperty("errorCode").GetString());
        Assert.True(conflictResponse.GetProperty("conflict").GetBoolean());
        Assert.False(conflictResponse.GetProperty("alreadyProcessed").GetBoolean());

        var missingEntitlement = await PostAsUserAsync(
            userId,
            "/api/cosmetics/items/frame_comet/claim",
            MobileEconomyContractPayloads.CosmeticItemClaim(
                idempotencyKey: $"cosmetic-claim-invalid-{Guid.NewGuid():N}",
                sourceType: "reward"));
        Assert.Equal(HttpStatusCode.Conflict, missingEntitlement.StatusCode);
        await AssertBusinessErrorCodeAsync(missingEntitlement, "not_eligible");
    }

    [Fact]
    public async Task CosmeticFragmentGrant_FlutterPayload_UsesCopiesAndIdempotencyRules()
    {
        var userId = $"mobile-contract-fragment-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 0, xp: 0);
        var itemId = await EnsureCosmeticItemAsync("frame_comet", "Comet Frame");
        var entitlement = await CosmeticEntitlementTestSeeder.SeedFragmentEntitlementAsync(
            _factory.Services,
            userId,
            itemId,
            1,
            "season_milestone",
            "season:5:milestone:9");

        var idempotencyKey = $"frag:{Guid.NewGuid():N}";
        var firstPayload = MobileEconomyContractPayloads.CosmeticFragmentGrant(
            idempotencyKey: idempotencyKey,
            fragmentName: "Comet Frame Fragment",
            copies: 1,
            entitlementId: entitlement.Id);

        var first = await PostAsUserAsync(
            userId,
            "/api/cosmetics/fragments/grant",
            firstPayload);

        Assert.NotEqual(HttpStatusCode.NotFound, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResponse = await ReadJsonAsync(first);
        Assert.True(firstResponse.GetProperty("success").GetBoolean());

        var replay = await PostAsUserAsync(
            userId,
            "/api/cosmetics/fragments/grant",
            firstPayload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayResponse = await ReadJsonAsync(replay);
        Assert.True(replayResponse.GetProperty("alreadyProcessed").GetBoolean());

        var conflict = await PostAsUserAsync(
            userId,
            "/api/cosmetics/fragments/grant",
            MobileEconomyContractPayloads.CosmeticFragmentGrant(
                idempotencyKey: idempotencyKey,
                fragmentName: "Comet Frame Fragment",
                copies: 2,
                entitlementId: entitlement.Id));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var conflictResponse = await ReadJsonAsync(conflict);
        Assert.Equal("entitlement_mismatch", conflictResponse.GetProperty("errorCode").GetString());

        var missingEntitlement = await PostAsUserAsync(
            userId,
            "/api/cosmetics/fragments/grant",
            MobileEconomyContractPayloads.CosmeticFragmentGrant(
                idempotencyKey: $"frag-invalid:{Guid.NewGuid():N}",
                fragmentName: "Comet Frame Fragment",
                copies: 0));
        Assert.Equal(HttpStatusCode.Conflict, missingEntitlement.StatusCode);
        await AssertBusinessErrorCodeAsync(missingEntitlement, "not_eligible");
    }

    [Fact]
    public async Task SeasonDailyRunClaim_FlutterPayload_SettlesAndReturnsBusinessErrors()
    {
        var userId = $"mobile-contract-season-daily-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 0, xp: 0);
        var seasonId = await EnsureActiveSeasonAsync();
        await SeedDailyRunChestClaimAsync(userId, "season-daily-mobile-tx-1", xp: 30);

        var idempotencyKey = $"season_daily_run/{userId}/{seasonId}/season-daily-mobile-tx-1";
        var firstPayload = MobileEconomyContractPayloads.SeasonDailyRunClaim(
            idempotencyKey: idempotencyKey,
            seasonId: seasonId,
            transactionId: "season-daily-mobile-tx-1",
            awardedXp: 30);

        var first = await PostAsUserAsync(userId, "/api/seasons/daily-run-claim", firstPayload);
        Assert.NotEqual(HttpStatusCode.NotFound, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResponse = await ReadJsonAsync(first);
        Assert.True(firstResponse.GetProperty("success").GetBoolean());
        Assert.Equal(30, firstResponse.GetProperty("awardedXp").GetInt32());
        Assert.Equal(30, firstResponse.GetProperty("season").GetProperty("earnedXp").GetInt32());

        var replay = await PostAsUserAsync(userId, "/api/seasons/daily-run-claim", firstPayload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayResponse = await ReadJsonAsync(replay);
        Assert.Equal(30, replayResponse.GetProperty("awardedXp").GetInt32());
        Assert.Equal(
            firstResponse.GetProperty("season").GetProperty("earnedXp").GetInt32(),
            replayResponse.GetProperty("season").GetProperty("earnedXp").GetInt32());
        Assert.Equal(
            firstResponse.GetProperty("awardedXp").GetInt32(),
            replayResponse.GetProperty("awardedXp").GetInt32());

        var conflict = await PostAsUserAsync(
            userId,
            "/api/seasons/daily-run-claim",
            MobileEconomyContractPayloads.SeasonDailyRunClaim(
                idempotencyKey: idempotencyKey,
                seasonId: seasonId,
                transactionId: "season-daily-mobile-tx-2",
                awardedXp: 30));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await AssertBusinessErrorCodeAsync(conflict, "idempotency_conflict");

        var invalidSeason = await PostAsUserAsync(
            userId,
            "/api/seasons/daily-run-claim",
            MobileEconomyContractPayloads.SeasonDailyRunClaim(
                idempotencyKey: $"season_daily_run/{userId}/999999/tx",
                seasonId: 999999,
                transactionId: "season-daily-mobile-tx-unused",
                awardedXp: 30));
        Assert.Equal(HttpStatusCode.Conflict, invalidSeason.StatusCode);
        await AssertBusinessErrorCodeAsync(invalidSeason, "invalid_season");
    }

    [Fact]
    public async Task DailyRunFragmentGrant_AfterSeasonClaim_IsIdempotentPerTransactionId()
    {
        var userId = $"mobile-contract-daily-frag-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 0, xp: 0);
        await EnsureCosmeticItemAsync("frame_comet", "Comet Frame");
        var seasonId = await EnsureActiveSeasonAsync();

        const string transactionId = "daily-run-mobile-frag-tx-1";
        await SeedDailyRunChestClaimAsync(userId, transactionId, xp: 30, fragmentCopies: 2);

        var seasonClaim = await PostAsUserAsync(
            userId,
            "/api/seasons/daily-run-claim",
            MobileEconomyContractPayloads.SeasonDailyRunClaim(
                idempotencyKey: $"season_daily_run/{userId}/{seasonId}/{transactionId}",
                seasonId: seasonId,
                transactionId: transactionId,
                awardedXp: 30));
        Assert.Equal(HttpStatusCode.OK, seasonClaim.StatusCode);
        var seasonPayload = await ReadJsonAsync(seasonClaim);
        Assert.NotNull(seasonPayload.GetProperty("fragmentGrant").GetProperty("fragmentName").GetString());
        Assert.Equal(2, seasonPayload.GetProperty("fragmentGrant").GetProperty("copies").GetInt32());

        var grantPayload = MobileEconomyContractPayloads.CosmeticFragmentGrant(
            idempotencyKey: transactionId,
            fragmentName: "Comet Frame Fragment",
            copies: 2,
            sourceType: "dailyRun",
            transactionId: transactionId);

        var firstGrant = await PostAsUserAsync(userId, "/api/cosmetics/fragments/grant", grantPayload);
        Assert.Equal(HttpStatusCode.OK, firstGrant.StatusCode);
        var firstGrantPayload = await ReadJsonAsync(firstGrant);
        Assert.False(firstGrantPayload.GetProperty("alreadyProcessed").GetBoolean());
        Assert.Equal(2, firstGrantPayload.GetProperty("progress").GetProperty("collectedFragments").GetInt32());

        var replayGrant = await PostAsUserAsync(userId, "/api/cosmetics/fragments/grant", grantPayload);
        Assert.Equal(HttpStatusCode.OK, replayGrant.StatusCode);
        var replayGrantPayload = await ReadJsonAsync(replayGrant);
        Assert.True(replayGrantPayload.GetProperty("alreadyProcessed").GetBoolean());
        Assert.Equal(2, replayGrantPayload.GetProperty("progress").GetProperty("collectedFragments").GetInt32());
        Assert.Equal(
            firstGrantPayload.GetProperty("progress").GetProperty("updatedAt").GetString(),
            replayGrantPayload.GetProperty("progress").GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task SeasonMilestoneClaim_FlutterPayload_SettlesAndAvoidsRouteDrift()
    {
        var userId = $"mobile-contract-season-ms-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 0, xp: 0);
        var seasonId = await EnsureActiveSeasonAsync();
        var milestoneId = await EnsureSeasonMilestoneAsync(seasonId, xpRequired: 50, rewardType: "coins", payloadJson: """{"coins":40}""");
        await SetSeasonXpAsync(userId, seasonId, earnedXp: 80);

        var idempotencyKey = $"season_milestone:{userId}:{seasonId}:{milestoneId}";
        var payload = MobileEconomyContractPayloads.SeasonMilestoneClaim(idempotencyKey, seasonId);

        var first = await PostAsUserAsync(userId, $"/api/seasons/milestones/{milestoneId}/claim", payload);
        Assert.NotEqual(HttpStatusCode.NotFound, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResponse = await ReadJsonAsync(first);
        Assert.True(firstResponse.GetProperty("success").GetBoolean());
        Assert.False(firstResponse.GetProperty("alreadyClaimed").GetBoolean());
        Assert.Equal(40, firstResponse.GetProperty("reward").GetProperty("coins").GetInt32());
        Assert.Contains(
            milestoneId,
            firstResponse.GetProperty("season").GetProperty("claimedMilestoneIds").EnumerateArray().Select(x => x.GetInt32()));

        var replay = await PostAsUserAsync(userId, $"/api/seasons/milestones/{milestoneId}/claim", payload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayResponse = await ReadJsonAsync(replay);
        Assert.Equal(
            firstResponse.GetProperty("reward").GetProperty("coins").GetInt32(),
            replayResponse.GetProperty("reward").GetProperty("coins").GetInt32());
        Assert.Equal(
            firstResponse.GetProperty("season").GetProperty("earnedXp").GetInt32(),
            replayResponse.GetProperty("season").GetProperty("earnedXp").GetInt32());

        var conflict = await PostAsUserAsync(
            userId,
            $"/api/seasons/milestones/{milestoneId}/claim",
            MobileEconomyContractPayloads.SeasonMilestoneClaim(idempotencyKey, seasonId + 1));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await AssertBusinessErrorCodeAsync(conflict, "idempotency_conflict");

        var invalidMilestone = await PostAsUserAsync(
            userId,
            "/api/seasons/milestones/999999/claim",
            MobileEconomyContractPayloads.SeasonMilestoneClaim(
                idempotencyKey: $"season_milestone:{userId}:{seasonId}:999999",
                seasonId: seasonId));
        Assert.Equal(HttpStatusCode.Conflict, invalidMilestone.StatusCode);
        await AssertBusinessErrorCodeAsync(invalidMilestone, "invalid_milestone");
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

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload;
    }

    private static async Task AssertBusinessErrorCodeAsync(HttpResponseMessage response, string expectedCode)
    {
        var payload = await ReadJsonAsync(response);
        Assert.True(payload.TryGetProperty("errorCode", out var errorCode), "Expected business error payload with errorCode.");
        Assert.Equal(expectedCode, errorCode.GetString());
    }

    private async Task EnsureUserAsync(string userId, int coins, int xp = 0)
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
        profile.UpdatedAt = DateTime.UtcNow;

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

    private async Task<int> EnsureActiveSeasonAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var season = new CosmeticSeason
        {
            Key = $"season-{Guid.NewGuid():N}",
            Name = "Contract Season",
            Status = CosmeticSeasonStatuses.Active,
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        db.CosmeticSeasons.Add(season);
        await db.SaveChangesAsync();
        return season.Id;
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
            FragmentLabel = key == "frame_comet" ? "Comet Frame Fragment" : null,
            FragmentsRequired = key == "frame_comet" ? 5 : null,
            IsActive = true,
            AssetVersion = "1"
        };
        db.CosmeticItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }
}

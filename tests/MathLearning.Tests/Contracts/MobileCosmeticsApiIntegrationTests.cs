using System.Net;
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

/// <summary>
/// End-to-end cosmetics API scenarios for mobile settlement, idempotency, fragments, avatar, and inventory.
/// </summary>
public sealed class MobileCosmeticsApiIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string FrameCometKey = "frame_comet";
    private const string FrameCometFragment = "Comet Frame Fragment";
    private const int FrameCometFragmentsRequired = 5;

    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public MobileCosmeticsApiIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ItemClaim_FirstTime_AddsItemToInventory()
    {
        var userId = NewUserId("claim-first");
        await EnsureUserAsync(userId);
        var itemId = await EnsureCosmeticItemAsync(FrameCometKey, "Comet Frame");
        var entitlement = await CosmeticEntitlementTestSeeder.SeedItemEntitlementAsync(
            _factory.Services,
            userId,
            itemId,
            "reward_track",
            "season:api:claim-first");

        var idempotencyKey = $"claim-first-{Guid.NewGuid():N}";
        var response = await PostAsUserAsync(
            userId,
            $"/api/cosmetics/items/{FrameCometKey}/claim",
            MobileEconomyContractPayloads.CosmeticItemClaim(
                idempotencyKey: idempotencyKey,
                sourceType: "reward",
                entitlementId: entitlement.Id,
                sourceEvent: "level:7"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        Assert.False(payload.GetProperty("alreadyClaimed").GetBoolean());
        Assert.Equal(FrameCometKey, payload.GetProperty("itemKey").GetString());

        var inventory = await GetInventoryAsync(userId);
        Assert.Contains(FrameCometKey, inventory.ItemKeys);
        Assert.Equal(1, await CountInventoryRowsAsync(userId, itemId));
    }

    [Fact]
    public async Task ItemClaim_SameIdempotencyKey_ReturnsAlreadyClaimed_WithoutDuplicateRow()
    {
        var userId = NewUserId("claim-replay");
        await EnsureUserAsync(userId);
        var itemId = await EnsureCosmeticItemAsync(FrameCometKey, "Comet Frame");
        var entitlement = await CosmeticEntitlementTestSeeder.SeedItemEntitlementAsync(
            _factory.Services,
            userId,
            itemId,
            "reward_track",
            "season:api:claim-replay");

        const string idempotencyKey = "claim-replay-key";
        var claimPayload = MobileEconomyContractPayloads.CosmeticItemClaim(
            idempotencyKey: idempotencyKey,
            sourceType: "reward",
            entitlementId: entitlement.Id,
            sourceEvent: "level:7");

        var first = await PostAsUserAsync(userId, $"/api/cosmetics/items/{FrameCometKey}/claim", claimPayload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstPayload = await ReadJsonAsync(first);
        Assert.False(firstPayload.GetProperty("alreadyClaimed").GetBoolean());
        var inventoryAfterFirst = await GetInventoryAsync(userId);

        var replay = await PostAsUserAsync(userId, $"/api/cosmetics/items/{FrameCometKey}/claim", claimPayload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayPayload = await ReadJsonAsync(replay);
        Assert.True(replayPayload.GetProperty("alreadyClaimed").GetBoolean());
        Assert.True(replayPayload.GetProperty("alreadyProcessed").GetBoolean());

        var inventoryAfterReplay = await GetInventoryAsync(userId);
        Assert.Equal(inventoryAfterFirst.ItemKeys, inventoryAfterReplay.ItemKeys);
        Assert.Equal(1, await CountInventoryRowsAsync(userId, itemId));
    }

    [Fact]
    public async Task ItemClaim_SameKeyDifferentSourceEvent_ReturnsIdempotencyConflict()
    {
        var userId = NewUserId("claim-conflict");
        await EnsureUserAsync(userId);
        var itemId = await EnsureCosmeticItemAsync(FrameCometKey, "Comet Frame");
        var entitlement = await CosmeticEntitlementTestSeeder.SeedItemEntitlementAsync(
            _factory.Services,
            userId,
            itemId,
            "reward_track",
            "season:api:claim-conflict");
        var secondEntitlement = await CosmeticEntitlementTestSeeder.SeedItemEntitlementAsync(
            _factory.Services,
            userId,
            itemId,
            "reward_track",
            "season:api:claim-conflict:2");

        const string idempotencyKey = "claim-conflict-key";
        var firstPayload = MobileEconomyContractPayloads.CosmeticItemClaim(
            idempotencyKey: idempotencyKey,
            sourceType: "reward",
            entitlementId: entitlement.Id,
            sourceEvent: "level:7",
            metadata: new Dictionary<string, object?> { ["sourceEvent"] = "level:7" });

        var first = await PostAsUserAsync(userId, $"/api/cosmetics/items/{FrameCometKey}/claim", firstPayload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var conflictPayload = MobileEconomyContractPayloads.CosmeticItemClaim(
            idempotencyKey: idempotencyKey,
            sourceType: "reward",
            entitlementId: secondEntitlement.Id,
            sourceEvent: "milestone:2",
            metadata: new Dictionary<string, object?> { ["sourceEvent"] = "milestone:2" });

        var conflict = await PostAsUserAsync(userId, $"/api/cosmetics/items/{FrameCometKey}/claim", conflictPayload);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var conflictJson = await ReadJsonAsync(conflict);
        Assert.Equal("idempotency_conflict", conflictJson.GetProperty("errorCode").GetString());
        Assert.True(conflictJson.GetProperty("conflict").GetBoolean());
    }

    [Fact]
    public async Task FragmentGrant_IncrementsProgress()
    {
        var userId = NewUserId("frag-increment");
        await EnsureUserAsync(userId);
        var itemId = await EnsureCosmeticItemAsync(FrameCometKey, "Comet Frame");
        var entitlement = await CosmeticEntitlementTestSeeder.SeedFragmentEntitlementAsync(
            _factory.Services,
            userId,
            itemId,
            2,
            "season_milestone",
            "season:api:frag-increment");

        const string idempotencyKey = "frag-increment-1";
        var response = await PostAsUserAsync(
            userId,
            "/api/cosmetics/fragments/grant",
            MobileEconomyContractPayloads.CosmeticFragmentGrant(
                idempotencyKey: idempotencyKey,
                fragmentName: FrameCometFragment,
                copies: 2,
                entitlementId: entitlement.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        Assert.Equal(2, payload.GetProperty("progress").GetProperty("collectedFragments").GetInt32());
        Assert.Equal(FrameCometFragmentsRequired, payload.GetProperty("progress").GetProperty("requiredFragments").GetInt32());

        var inventory = await GetInventoryAsync(userId);
        Assert.Equal(2, inventory.FragmentProgress[FrameCometFragment]);
    }

    [Fact]
    public async Task FragmentGrant_SameTransactionId_ReturnsAlreadyProcessed_NoDoubleIncrement()
    {
        var userId = NewUserId("frag-replay");
        await EnsureUserAsync(userId);
        var itemId = await EnsureCosmeticItemAsync(FrameCometKey, "Comet Frame");
        var entitlement = await CosmeticEntitlementTestSeeder.SeedFragmentEntitlementAsync(
            _factory.Services,
            userId,
            itemId,
            2,
            "season_milestone",
            "season:api:frag-replay");

        const string transactionId = "frag-tx-replay-001";
        var grantPayload = MobileEconomyContractPayloads.CosmeticFragmentGrant(
            idempotencyKey: transactionId,
            fragmentName: FrameCometFragment,
            copies: 2,
            entitlementId: entitlement.Id,
            transactionId: transactionId);

        var first = await PostAsUserAsync(userId, "/api/cosmetics/fragments/grant", grantPayload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstPayload = await ReadJsonAsync(first);
        Assert.Equal(2, firstPayload.GetProperty("progress").GetProperty("collectedFragments").GetInt32());

        var replay = await PostAsUserAsync(userId, "/api/cosmetics/fragments/grant", grantPayload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayPayload = await ReadJsonAsync(replay);
        Assert.True(replayPayload.GetProperty("alreadyProcessed").GetBoolean());
        Assert.Equal(2, replayPayload.GetProperty("progress").GetProperty("collectedFragments").GetInt32());
        Assert.Equal(
            firstPayload.GetProperty("progress").GetProperty("updatedAt").GetString(),
            replayPayload.GetProperty("progress").GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task FragmentGrant_AtThreshold_UnlocksItemInInventory()
    {
        var userId = NewUserId("frag-unlock");
        await EnsureUserAsync(userId);
        var itemId = await EnsureCosmeticItemAsync(FrameCometKey, "Comet Frame");
        var entitlement = await CosmeticEntitlementTestSeeder.SeedFragmentEntitlementAsync(
            _factory.Services,
            userId,
            itemId,
            FrameCometFragmentsRequired,
            "season_milestone",
            "season:api:frag-unlock");

        var response = await PostAsUserAsync(
            userId,
            "/api/cosmetics/fragments/grant",
            MobileEconomyContractPayloads.CosmeticFragmentGrant(
                idempotencyKey: $"frag-unlock-{Guid.NewGuid():N}",
                fragmentName: FrameCometFragment,
                copies: FrameCometFragmentsRequired,
                entitlementId: entitlement.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        Assert.True(payload.GetProperty("itemUnlocked").GetBoolean());
        Assert.Equal(FrameCometKey, payload.GetProperty("unlockedItemId").GetString());

        var inventory = await GetInventoryAsync(userId);
        Assert.Contains(FrameCometKey, inventory.ItemKeys);
        Assert.Equal(1, await CountInventoryRowsAsync(userId, itemId));
    }

    [Fact]
    public async Task AvatarPut_UnownedItem_Returns403()
    {
        var userId = NewUserId("avatar-403");
        await EnsureUserAsync(userId);
        var itemId = await EnsureCosmeticItemAsync(FrameCometKey, "Comet Frame");
        await EnsureCosmeticItemAsync("frame_not_owned", "Locked Frame");
        var entitlement = await CosmeticEntitlementTestSeeder.SeedItemEntitlementAsync(
            _factory.Services,
            userId,
            itemId,
            "reward_track",
            "season:api:avatar-403");

        await PostAsUserAsync(
            userId,
            $"/api/cosmetics/items/{FrameCometKey}/claim",
            MobileEconomyContractPayloads.CosmeticItemClaim(
                idempotencyKey: $"avatar-claim-{Guid.NewGuid():N}",
                sourceType: "reward",
                entitlementId: entitlement.Id));

        var response = await PutAsUserAsync(
            userId,
            "/api/cosmetics/avatar",
            new Dictionary<string, object?>
            {
                ["slots"] = new Dictionary<string, string?> { ["frame"] = "frame_not_owned" }
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AvatarPut_OwnedItems_PersistedAndGetReturnsSameSlots()
    {
        var userId = NewUserId("avatar-persist");
        await EnsureUserAsync(userId);
        var itemId = await EnsureCosmeticItemAsync(FrameCometKey, "Comet Frame", category: "frame");
        var entitlement = await CosmeticEntitlementTestSeeder.SeedItemEntitlementAsync(
            _factory.Services,
            userId,
            itemId,
            "reward_track",
            "season:api:avatar-persist");

        await PostAsUserAsync(
            userId,
            $"/api/cosmetics/items/{FrameCometKey}/claim",
            MobileEconomyContractPayloads.CosmeticItemClaim(
                idempotencyKey: $"avatar-persist-claim-{Guid.NewGuid():N}",
                sourceType: "reward",
                entitlementId: entitlement.Id));

        var putResponse = await PutAsUserAsync(
            userId,
            "/api/cosmetics/avatar",
            new Dictionary<string, object?>
            {
                ["slots"] = new Dictionary<string, string?> { ["frame"] = FrameCometKey }
            });
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var putPayload = await ReadJsonAsync(putResponse);
        Assert.Equal(FrameCometKey, putPayload.GetProperty("slots").GetProperty("frame").GetString());

        var getResponse = await GetAsUserAsync(userId, "/api/cosmetics/avatar");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getPayload = await ReadJsonAsync(getResponse);
        Assert.Equal(FrameCometKey, getPayload.GetProperty("slots").GetProperty("frame").GetString());
        Assert.Equal(
            putPayload.GetProperty("version").GetInt64(),
            getPayload.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task AvatarPut_LegacySnakeCaseBody_Returns400()
    {
        var userId = NewUserId("avatar-legacy-shape");
        await EnsureUserAsync(userId);
        await EnsureCosmeticItemAsync(FrameCometKey, "Comet Frame", category: "frame");

        var response = await PutAsUserAsync(
            userId,
            "/api/cosmetics/avatar",
            new Dictionary<string, object?>
            {
                ["skin_id"] = "skin_default",
                ["frame_id"] = FrameCometKey,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InventoryGet_AfterUnlock_ReflectsAuthoritativeServerState()
    {
        var userId = NewUserId("inventory-authoritative");
        await EnsureUserAsync(userId);
        var itemId = await EnsureCosmeticItemAsync(FrameCometKey, "Comet Frame");
        var entitlement = await CosmeticEntitlementTestSeeder.SeedFragmentEntitlementAsync(
            _factory.Services,
            userId,
            itemId,
            FrameCometFragmentsRequired,
            "season_milestone",
            "season:api:inventory-authoritative");

        var grant = await PostAsUserAsync(
            userId,
            "/api/cosmetics/fragments/grant",
            MobileEconomyContractPayloads.CosmeticFragmentGrant(
                idempotencyKey: $"inventory-unlock-{Guid.NewGuid():N}",
                fragmentName: FrameCometFragment,
                copies: FrameCometFragmentsRequired,
                entitlementId: entitlement.Id));
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
        var grantPayload = await ReadJsonAsync(grant);
        Assert.True(grantPayload.GetProperty("itemUnlocked").GetBoolean());

        var inventory = await GetInventoryAsync(userId);
        Assert.Contains(FrameCometKey, inventory.ItemKeys);
        Assert.Equal(FrameCometFragmentsRequired, inventory.FragmentProgress[FrameCometFragment]);
        Assert.Equal(1, inventory.ItemKeys.Count(key => key == FrameCometKey));
    }

    private static string NewUserId(string prefix) => $"mobile-cosmetics-{prefix}-{Guid.NewGuid():N}";

    private async Task<(IReadOnlyList<string> ItemKeys, IReadOnlyDictionary<string, int> FragmentProgress)> GetInventoryAsync(
        string userId)
    {
        var response = await GetAsUserAsync(userId, "/api/cosmetics/inventory");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        var itemKeys = payload.GetProperty("itemKeys").EnumerateArray().Select(x => x.GetString()!).ToList();
        var fragmentProgress = payload.GetProperty("fragmentProgress").EnumerateObject()
            .ToDictionary(x => x.Name, x => x.Value.GetInt32(), StringComparer.OrdinalIgnoreCase);
        return (itemKeys, fragmentProgress);
    }

    private async Task<int> CountInventoryRowsAsync(string userId, int cosmeticItemId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserCosmeticInventories.CountAsync(
            x => x.UserId == userId && x.CosmeticItemId == cosmeticItemId && !x.IsRevoked);
    }

    private async Task<HttpResponseMessage> GetAsUserAsync(string userId, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Test-UserId", userId);
        return await _client.SendAsync(request);
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

    private async Task<HttpResponseMessage> PutAsUserAsync(string userId, string url, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private async Task EnsureUserAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (await userManager.FindByIdAsync(userId) is null)
        {
            await userManager.CreateAsync(new IdentityUser { Id = userId, UserName = userId });
        }

        if (!await db.UserProfiles.AnyAsync(x => x.UserId == userId))
        {
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Coins = 0,
                Xp = 0,
                Level = 1,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    private async Task<int> EnsureCosmeticItemAsync(string key, string name, string category = "frame")
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
            Category = category,
            Rarity = "rare",
            AssetPath = $"cosmetics/{category}/{key}",
            UnlockType = CosmeticUnlockTypes.RewardRule,
            FragmentLabel = key == FrameCometKey ? FrameCometFragment : null,
            FragmentsRequired = key == FrameCometKey ? FrameCometFragmentsRequired : null,
            IsActive = true,
            AssetVersion = "1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.CosmeticItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Contracts;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Contracts;

public sealed class MobileCosmeticsContractIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public MobileCosmeticsContractIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Catalog_ReturnsPublishedMetadata_WithCacheHeaders()
    {
        var userId = $"mobile-cosmetics-catalog-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId);
        await EnsureCosmeticItemAsync("frame_comet", "Comet Frame", category: "frame");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/cosmetics/catalog");
        request.Headers.Add("X-Test-UserId", userId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("ETag", out var etagValues));
        Assert.Contains("catalog-", etagValues.Single(), StringComparison.Ordinal);
        Assert.Contains("max-age=300", response.Headers.CacheControl?.ToString());
        Assert.Contains("private", response.Headers.CacheControl?.ToString());

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.GetProperty("catalogVersion").GetString()?.StartsWith("catalog-", StringComparison.Ordinal));
        Assert.Contains(
            payload.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("key").GetString() == "frame_comet");
    }

    [Fact]
    public async Task Inventory_ReturnsItemKeysAndFragmentProgress()
    {
        var userId = $"mobile-cosmetics-inventory-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId);
        await EnsureCosmeticItemAsync("frame_comet", "Comet Frame");

        await PostAsUserAsync(
            userId,
            "/api/cosmetics/items/frame_comet/claim",
            MobileEconomyContractPayloads.CosmeticItemClaim(
                idempotencyKey: $"inventory-claim-{Guid.NewGuid():N}",
                sourceType: "reward",
                operationId: $"inventory-claim-{Guid.NewGuid():N}"));

        await PostAsUserAsync(
            userId,
            "/api/cosmetics/fragments/grant",
            MobileEconomyContractPayloads.CosmeticFragmentGrant(
                idempotencyKey: $"inventory-fragment-{Guid.NewGuid():N}",
                fragmentName: "Comet Frame Fragment",
                copies: 2,
                operationId: $"inventory-fragment-{Guid.NewGuid():N}"));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/cosmetics/inventory");
        request.Headers.Add("X-Test-UserId", userId);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("frame_comet", payload.GetProperty("itemKeys").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(2, payload.GetProperty("fragmentProgress").GetProperty("Comet Frame Fragment").GetInt32());
    }

    [Fact]
    public async Task AvatarPut_ValidatesOwnership_AndReturnsEquippedSlots()
    {
        var userId = $"mobile-cosmetics-avatar-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId);
        await EnsureCosmeticItemAsync("frame_comet", "Comet Frame", category: "frame");
        await EnsureCosmeticItemAsync("frame_not_owned", "Locked Frame", category: "frame");

        await PostAsUserAsync(
            userId,
            "/api/cosmetics/items/frame_comet/claim",
            MobileEconomyContractPayloads.CosmeticItemClaim(
                idempotencyKey: $"avatar-claim-{Guid.NewGuid():N}",
                sourceType: "reward"));

        var update = await SendAsUserAsync(
            userId,
            HttpMethod.Put,
            "/api/cosmetics/avatar",
            new Dictionary<string, object?>
            {
                ["slots"] = new Dictionary<string, string?>
                {
                    ["frame"] = "frame_comet"
                }
            });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("frame_comet", updated.GetProperty("slots").GetProperty("frame").GetString());
        Assert.True(updated.GetProperty("version").GetInt64() > 0);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/cosmetics/avatar");
        getRequest.Headers.Add("X-Test-UserId", userId);
        var getResponse = await _client.SendAsync(getRequest);
        var avatar = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("frame_comet", avatar.GetProperty("slots").GetProperty("frame").GetString());

        var invalid = await SendAsUserAsync(
            userId,
            HttpMethod.Put,
            "/api/cosmetics/avatar",
            new Dictionary<string, object?>
            {
                ["slots"] = new Dictionary<string, string?>
                {
                    ["frame"] = "frame_not_owned"
                }
            });
        Assert.Equal(HttpStatusCode.Forbidden, invalid.StatusCode);
    }

    [Fact]
    public async Task FragmentGrant_DailyRunTransactionId_IsAcceptedAsIdempotencyKey()
    {
        var userId = $"mobile-cosmetics-daily-frag-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId);
        await EnsureCosmeticItemAsync("frame_comet", "Comet Frame");
        await SeedDailyRunChestClaimAsync(userId, "daily-run-chest-tx-12345", fragmentCopies: 1);
        await SeedSeasonDailyRunClaimAsync(userId, "daily-run-chest-tx-12345");

        const string transactionId = "daily-run-chest-tx-12345";
        var payload = MobileEconomyContractPayloads.CosmeticFragmentGrant(
            idempotencyKey: transactionId,
            fragmentName: "Comet Frame Fragment",
            copies: 1,
            sourceType: "dailyRun",
            transactionId: transactionId);

        var first = await PostAsUserAsync(userId, "/api/cosmetics/fragments/grant", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await PostAsUserAsync(userId, "/api/cosmetics/fragments/grant", payload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayPayload = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(replayPayload.GetProperty("alreadyProcessed").GetBoolean());
        Assert.Equal(1, replayPayload.GetProperty("progress").GetProperty("collectedFragments").GetInt32());
    }

    private async Task<HttpResponseMessage> PostAsUserAsync(string userId, string url, object payload)
    {
        return await SendAsUserAsync(userId, HttpMethod.Post, url, payload);
    }

    private async Task<HttpResponseMessage> SendAsUserAsync(
        string userId,
        HttpMethod method,
        string url,
        object payload)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await _client.SendAsync(request);
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

    private async Task EnsureCosmeticItemAsync(
        string key,
        string name,
        string category = "frame")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        if (await db.CosmeticItems.AnyAsync(x => x.Key == key))
            return;

        db.CosmeticItems.Add(new CosmeticItem
        {
            Key = key,
            Name = name,
            Category = category,
            Rarity = "rare",
            AssetPath = $"cosmetics/{category}/{key}",
            UnlockType = CosmeticUnlockTypes.RewardRule,
            FragmentLabel = key == "frame_comet" ? "Comet Frame Fragment" : null,
            FragmentsRequired = key == "frame_comet" ? 5 : null,
            IsActive = true,
            AssetVersion = "1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedDailyRunChestClaimAsync(string userId, string transactionId, int fragmentCopies)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        if (await db.DailyRunChestClaims.AnyAsync(x => x.UserId == userId && x.TransactionId == transactionId))
            return;

        db.DailyRunChestClaims.Add(new DailyRunChestClaim
        {
            UserId = userId,
            Day = DateOnly.FromDateTime(DateTime.UtcNow),
            TransactionId = transactionId,
            Xp = 25,
            Coins = 10,
            CosmeticFragment = "Comet Frame Fragment",
            FragmentCopies = fragmentCopies,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedSeasonDailyRunClaimAsync(string userId, string transactionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        if (await db.UserSeasonDailyRunClaims.AnyAsync(x => x.UserId == userId && x.DailyRunTransactionId == transactionId))
            return;

        var seasonId = await db.CosmeticSeasons.Where(x => x.IsActive).Select(x => x.Id).FirstOrDefaultAsync();
        if (seasonId == 0)
        {
            var season = new CosmeticSeason
            {
                Key = $"test-{Guid.NewGuid():N}",
                Name = $"Test Season {Guid.NewGuid():N}",
                IsActive = true,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.CosmeticSeasons.Add(season);
            await db.SaveChangesAsync();
            seasonId = season.Id;
        }

        db.UserSeasonDailyRunClaims.Add(new UserSeasonDailyRunClaim
        {
            UserId = userId,
            SeasonId = seasonId,
            DailyRunTransactionId = transactionId,
            AwardedXp = 25,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

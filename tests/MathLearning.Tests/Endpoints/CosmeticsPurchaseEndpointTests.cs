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

namespace MathLearning.Tests.Endpoints;

public sealed class CosmeticsPurchaseEndpointTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public CosmeticsPurchaseEndpointTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Purchase_ReplaysSameIdempotencyKey_WithoutDoubleCharge()
    {
        var userId = $"purchase-replay-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 200);
        var itemId = await EnsurePurchasableItemAsync("shop_frame", "Shop Frame", 80);

        var payload = MobileEconomyContractPayloads.CosmeticPurchase("purchase-replay-key", itemId);
        var first = await PostAsUserAsync(userId, "/api/cosmetics/purchase", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await PostAsUserAsync(userId, "/api/cosmetics/purchase", payload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);

        Assert.Equal(1, await CountOwnedItemAsync(userId, itemId));
        Assert.Equal(120, await GetCoinsAsync(userId));
    }

    [Fact]
    public async Task Purchase_HiddenItem_IsRejectedWithoutCharge()
    {
        var userId = $"purchase-hidden-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId, coins: 200);
        var itemId = await EnsurePurchasableItemAsync("hidden_frame", "Hidden Frame", 80, isHidden: true);

        var response = await PostAsUserAsync(
            userId,
            "/api/cosmetics/purchase",
            MobileEconomyContractPayloads.CosmeticPurchase("purchase-hidden-key", itemId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_purchasable", payload.GetProperty("errorCode").GetString());
        Assert.Equal(0, await CountOwnedItemAsync(userId, itemId));
        Assert.Equal(200, await GetCoinsAsync(userId));
    }

    private async Task<HttpResponseMessage> PostAsUserAsync(string userId, string url, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await client.SendAsync(request);
    }

    private async Task EnsureUserAsync(string userId, int coins)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (await userManager.FindByIdAsync(userId) is null)
        {
            await userManager.CreateAsync(new IdentityUser { Id = userId, UserName = userId });
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                Coins = coins,
                UpdatedAt = DateTime.UtcNow
            };
            db.UserProfiles.Add(profile);
        }
        else
        {
            profile.Coins = coins;
            profile.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private async Task<int> EnsurePurchasableItemAsync(string key, string name, int coinPrice, bool isHidden = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var existing = await db.CosmeticItems.FirstOrDefaultAsync(x => x.Key == key);
        if (existing is not null)
        {
            existing.CoinPrice = coinPrice;
            existing.IsHidden = isHidden;
            existing.IsActive = true;
            existing.IsDefault = false;
            existing.ReleaseDate = DateTime.UtcNow.AddDays(-1);
            existing.RetirementDate = null;
            await db.SaveChangesAsync();
            return existing.Id;
        }

        var item = new CosmeticItem
        {
            Key = key,
            Name = name,
            Category = "frame",
            Rarity = "rare",
            AssetPath = $"cosmetics/frame/{key}",
            CoinPrice = coinPrice,
            IsHidden = isHidden,
            IsActive = true,
            IsDefault = false,
            ReleaseDate = DateTime.UtcNow.AddDays(-1),
            AssetVersion = "1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.CosmeticItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task<int> CountOwnedItemAsync(string userId, int cosmeticItemId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserCosmeticInventories.CountAsync(x => x.UserId == userId && x.CosmeticItemId == cosmeticItemId && !x.IsRevoked);
    }

    private async Task<int> GetCoinsAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserProfiles.Where(x => x.UserId == userId).Select(x => x.Coins).SingleAsync();
    }
}

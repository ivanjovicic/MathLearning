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

namespace MathLearning.Tests.Idempotency;

/// <summary>
/// Regression tests for cosmetics mutation response inventory and sourceEvent idempotency hashing.
/// </summary>
public sealed class CosmeticsMutationResponseTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string FrameCometKey = "frame_comet";
    private const string FrameCometFragment = "Comet Frame Fragment";
    private const int FrameCometFragmentsRequired = 5;

    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public CosmeticsMutationResponseTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ItemClaim_ResponseInventory_IncludesNewlyGrantedItem()
    {
        var userId = NewUserId("item-inventory");
        await EnsureUserAsync(userId);
        await EnsureCosmeticItemAsync(FrameCometKey, "Comet Frame");

        var response = await PostAsUserAsync(
            userId,
            $"/api/cosmetics/items/{FrameCometKey}/claim",
            MobileEconomyContractPayloads.CosmeticItemClaim(
                idempotencyKey: $"claim-inv-{Guid.NewGuid():N}",
                sourceType: "reward",
                sourceEvent: "level:7"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        var inventory = payload.GetProperty("inventory").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains(FrameCometKey, inventory);
    }

    [Fact]
    public async Task FragmentGrant_ResponseInventory_IncludesUnlockedItem()
    {
        var userId = NewUserId("frag-inventory");
        await EnsureUserAsync(userId);
        await EnsureCosmeticItemAsync(FrameCometKey, "Comet Frame");

        var response = await PostAsUserAsync(
            userId,
            "/api/cosmetics/fragments/grant",
            MobileEconomyContractPayloads.CosmeticFragmentGrant(
                idempotencyKey: $"frag-inv-{Guid.NewGuid():N}",
                fragmentName: FrameCometFragment,
                copies: FrameCometFragmentsRequired));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        Assert.True(payload.GetProperty("itemUnlocked").GetBoolean());
        var inventory = payload.GetProperty("inventory").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains(FrameCometKey, inventory);
        Assert.Equal(
            FrameCometFragmentsRequired,
            payload.GetProperty("fragmentProgress").GetProperty(FrameCometFragment).GetInt32());
    }

    [Fact]
    public async Task ItemClaim_TopLevelSourceEventOnly_ConflictsWithoutMetadataChange()
    {
        var userId = NewUserId("source-event-hash");
        await EnsureUserAsync(userId);
        await EnsureCosmeticItemAsync(FrameCometKey, "Comet Frame");

        const string idempotencyKey = "claim-source-event-only";
        var first = await PostAsUserAsync(
            userId,
            $"/api/cosmetics/items/{FrameCometKey}/claim",
            new Dictionary<string, object?>
            {
                ["idempotencyKey"] = idempotencyKey,
                ["operationId"] = idempotencyKey,
                ["source"] = "reward",
                ["sourceType"] = "reward",
                ["sourceEvent"] = "level:7"
            });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var conflict = await PostAsUserAsync(
            userId,
            $"/api/cosmetics/items/{FrameCometKey}/claim",
            new Dictionary<string, object?>
            {
                ["idempotencyKey"] = idempotencyKey,
                ["operationId"] = idempotencyKey,
                ["source"] = "reward",
                ["sourceType"] = "reward",
                ["sourceEvent"] = "milestone:2"
            });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var conflictPayload = await ReadJsonAsync(conflict);
        Assert.Equal("idempotency_conflict", conflictPayload.GetProperty("errorCode").GetString());
        Assert.True(conflictPayload.GetProperty("conflict").GetBoolean());
    }

    private static string NewUserId(string suffix) => $"cosmetics-mutation-{suffix}-{Guid.NewGuid():N}";

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
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task EnsureUserAsync(string userId)
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

        if (!await db.UserProfiles.AnyAsync(x => x.UserId == userId))
        {
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Username = userId,
                DisplayName = userId,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task EnsureCosmeticItemAsync(string key, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        if (await db.CosmeticItems.AnyAsync(x => x.Key == key))
            return;

        db.CosmeticItems.Add(new CosmeticItem
        {
            Key = key,
            Name = name,
            Category = "frame",
            Rarity = "rare",
            AssetPath = $"cosmetics/frame/{key}",
            UnlockType = CosmeticUnlockTypes.RewardRule,
            FragmentLabel = FrameCometFragment,
            FragmentsRequired = FrameCometFragmentsRequired,
            IsActive = true,
            AssetVersion = "1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

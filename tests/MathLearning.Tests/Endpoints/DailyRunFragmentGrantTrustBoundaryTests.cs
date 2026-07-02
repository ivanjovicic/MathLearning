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

public sealed class DailyRunFragmentGrantTrustBoundaryTests :
    IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public DailyRunFragmentGrantTrustBoundaryTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task MissingChestClaim_ReturnsNotEligible_AndCreatesNoProgress()
    {
        var userId = NewUserId("missing-chest");
        var transactionId = NewTransactionId("missing-chest");
        await EnsureUserAsync(userId);

        var response = await PostDailyRunGrantAsync(
            userId,
            transactionId,
            clientFragmentName: "Forged Fragment",
            clientCopies: 999);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertErrorCodeAsync(response, "not_eligible");
        Assert.Equal(0, await CountFragmentProgressAsync(userId));
        Assert.Equal(0, await CountIdempotencyEntriesAsync(userId, transactionId));
    }

    [Fact]
    public async Task ChestWithoutSeasonSettlement_ReturnsNotEligible_AndCreatesNoProgress()
    {
        var userId = NewUserId("missing-season");
        var transactionId = NewTransactionId("missing-season");
        await EnsureUserAsync(userId);
        await EnsureCosmeticItemAsync();
        await SeedChestClaimAsync(userId, transactionId, "Comet Frame Fragment", 2);

        var response = await PostDailyRunGrantAsync(
            userId,
            transactionId,
            clientFragmentName: "Forged Fragment",
            clientCopies: 999);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertErrorCodeAsync(response, "not_eligible");
        Assert.Equal(0, await CountFragmentProgressAsync(userId));
        Assert.Equal(0, await CountIdempotencyEntriesAsync(userId, transactionId));
    }

    [Fact]
    public async Task OtherUsersChestAndSeasonSettlement_CannotAuthorizeGrant()
    {
        var ownerId = NewUserId("owner");
        var attackerId = NewUserId("attacker");
        var transactionId = NewTransactionId("cross-user");
        await EnsureUserAsync(ownerId);
        await EnsureUserAsync(attackerId);
        await EnsureCosmeticItemAsync();
        await SeedChestClaimAsync(ownerId, transactionId, "Comet Frame Fragment", 2);
        await SeedSeasonSettlementAsync(ownerId, transactionId);

        var response = await PostDailyRunGrantAsync(
            attackerId,
            transactionId,
            clientFragmentName: "Comet Frame Fragment",
            clientCopies: 2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertErrorCodeAsync(response, "not_eligible");
        Assert.Equal(0, await CountFragmentProgressAsync(attackerId));
        Assert.Equal(0, await CountIdempotencyEntriesAsync(attackerId, transactionId));
    }

    [Fact]
    public async Task ValidGrant_IgnoresClientFragmentAndCopies_AndUsesStoredChestReward()
    {
        var userId = NewUserId("server-authority");
        var transactionId = NewTransactionId("server-authority");
        var itemId = await EnsureCosmeticItemAsync();
        await EnsureUserAsync(userId);
        await SeedChestClaimAsync(userId, transactionId, "Comet Frame Fragment", 2);
        await SeedSeasonSettlementAsync(userId, transactionId);

        var response = await PostDailyRunGrantAsync(
            userId,
            transactionId,
            clientFragmentName: "Forged Fragment",
            clientCopies: 999);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.False(payload.GetProperty("alreadyProcessed").GetBoolean());
        Assert.Equal(2, payload.GetProperty("progress").GetProperty("collectedFragments").GetInt32());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var progress = await db.UserCosmeticFragmentProgresses
            .AsNoTracking()
            .SingleAsync(x => x.UserId == userId && x.CosmeticItemId == itemId);
        Assert.Equal(2, progress.Collected);
        Assert.Equal(1, await db.CosmeticsIdempotencyLedgers.CountAsync(x =>
            x.UserId == userId &&
            x.OperationId == transactionId &&
            x.IdempotencyKey == transactionId));
    }

    [Fact]
    public async Task Replay_DoesNotGrantStoredCopiesTwice()
    {
        var userId = NewUserId("replay");
        var transactionId = NewTransactionId("replay");
        var itemId = await EnsureCosmeticItemAsync();
        await EnsureUserAsync(userId);
        await SeedChestClaimAsync(userId, transactionId, "Comet Frame Fragment", 2);
        await SeedSeasonSettlementAsync(userId, transactionId);

        var first = await PostDailyRunGrantAsync(userId, transactionId, "Forged Fragment", 999);
        var replay = await PostDailyRunGrantAsync(userId, transactionId, "Another Forged Fragment", 1);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayPayload = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(replayPayload.GetProperty("alreadyProcessed").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var progress = await db.UserCosmeticFragmentProgresses
            .AsNoTracking()
            .SingleAsync(x => x.UserId == userId && x.CosmeticItemId == itemId);
        Assert.Equal(2, progress.Collected);
    }

    private async Task<HttpResponseMessage> PostDailyRunGrantAsync(
        string userId,
        string transactionId,
        string clientFragmentName,
        int clientCopies)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/cosmetics/fragments/grant")
        {
            Content = JsonContent.Create(new
            {
                operationId = transactionId,
                idempotencyKey = transactionId,
                transactionId,
                fragmentName = clientFragmentName,
                copies = clientCopies,
                sourceType = "dailyRun",
                sourceEvent = "daily-run-complete"
            })
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await client.SendAsync(request);
    }

    private async Task EnsureUserAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
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
                Coins = 0,
                Xp = 0,
                Level = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task<int> EnsureCosmeticItemAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var existing = await db.CosmeticItems.FirstOrDefaultAsync(x => x.Key == "frame_comet");
        if (existing is not null)
            return existing.Id;

        var item = new CosmeticItem
        {
            Key = "frame_comet",
            Name = "Comet Frame",
            Category = CosmeticCategories.Frame,
            Rarity = "common",
            AssetPath = "/assets/test.png",
            UnlockType = CosmeticUnlockTypes.RewardRule,
            FragmentLabel = "Comet Frame Fragment",
            FragmentsRequired = 5,
            IsActive = true,
            AssetVersion = "1"
        };
        db.CosmeticItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task SeedChestClaimAsync(
        string userId,
        string transactionId,
        string fragmentName,
        int copies)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        db.DailyRunChestClaims.Add(new DailyRunChestClaim
        {
            UserId = userId,
            Day = DateOnly.FromDateTime(DateTime.UtcNow),
            TransactionId = transactionId,
            Xp = 30,
            Coins = 10,
            CosmeticFragment = fragmentName,
            FragmentCopies = copies,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedSeasonSettlementAsync(string userId, string transactionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var season = new CosmeticSeason
        {
            Key = $"test-season-{Guid.NewGuid():N}",
            Name = "Test Season",
            Status = CosmeticSeasonStatuses.Active,
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        db.CosmeticSeasons.Add(season);
        await db.SaveChangesAsync();

        db.UserSeasonDailyRunClaims.Add(new UserSeasonDailyRunClaim
        {
            UserId = userId,
            SeasonId = season.Id,
            DailyRunTransactionId = transactionId,
            AwardedXp = 30,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> CountFragmentProgressAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserCosmeticFragmentProgresses.CountAsync(x => x.UserId == userId);
    }

    private async Task<int> CountIdempotencyEntriesAsync(string userId, string transactionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.CosmeticsIdempotencyLedgers.CountAsync(x =>
            x.UserId == userId &&
            (x.OperationId == transactionId || x.IdempotencyKey == transactionId));
    }

    private static async Task AssertErrorCodeAsync(HttpResponseMessage response, string expected)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected, payload.GetProperty("errorCode").GetString());
    }

    private static string NewUserId(string suffix)
        => $"daily-fragment-{suffix}-{Guid.NewGuid():N}";

    private static string NewTransactionId(string suffix)
        => $"daily-fragment-tx-{suffix}-{Guid.NewGuid():N}";
}

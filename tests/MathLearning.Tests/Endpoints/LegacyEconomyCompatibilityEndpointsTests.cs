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

public sealed class LegacyEconomyCompatibilityEndpointsTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public LegacyEconomyCompatibilityEndpointsTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task CoinsEarn_LegacyRoute_IsGone_AndDoesNotMint()
    {
        var userId = NewUserId("earn");
        await EnsureUserAsync(userId, coins: 25);

        var response = await PostAsUserAsync(userId, "/api/coins/earn", new { amount = 50, reason = "forged" });
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal(25, await GetCoinsAsync(userId));
    }

    [Fact]
    public async Task CoinsSpend_LegacyRoute_IsGone_AndNegativeAmountCannotMint()
    {
        var userId = NewUserId("spend");
        await EnsureUserAsync(userId, coins: 25);

        var response = await PostAsUserAsync(userId, "/api/coins/spend", new { amount = -50, reason = "forged" });
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal(25, await GetCoinsAsync(userId));
    }

    [Fact]
    public async Task PowerupBuy_LegacyRoute_IsGone_AndDoesNotSpendOrIncrement()
    {
        var userId = NewUserId("powerup");
        await EnsureUserAsync(userId, coins: 100, streakFreezes: 0);

        var response = await PostAsUserAsync(userId, "/api/powerups/streak-freeze/buy", new { });
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal(100, await GetCoinsAsync(userId));
        Assert.Equal(0, await GetStreakFreezeCountAsync(userId));
    }

    [Fact]
    public async Task CanonicalHintFormulaGet_IsReadOnly_WhenLocked()
    {
        var userId = NewUserId("hint-read");
        await EnsureUserAsync(userId, coins: 100);
        var questionId = await EnsureQuestionWithHintsAsync();
        var beforeHints = await CountUserHintsAsync(userId);
        var beforeCoins = await GetCoinsAsync(userId);

        var response = await GetAsUserAsync(userId, $"/api/hints/questions/{questionId}/formula");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.GetProperty("requiresUnlock").GetBoolean());
        Assert.Equal(beforeHints, await CountUserHintsAsync(userId));
        Assert.Equal(beforeCoins, await GetCoinsAsync(userId));
    }

    [Fact]
    public async Task CanonicalHintFormulaGet_AfterUnlock_ReturnsContentWithoutAdditionalWrite()
    {
        var userId = NewUserId("hint-unlocked");
        await EnsureUserAsync(userId, coins: 100);
        var questionId = await EnsureQuestionWithHintsAsync();

        var unlock = await PostAsUserAsync(userId, "/api/economy/hints/use", new
        {
            operationId = "hint-unlock-op",
            idempotencyKey = "hint-unlock-key",
            questionId,
            hintType = "formula",
            costCoins = 999
        });
        Assert.Equal(HttpStatusCode.OK, unlock.StatusCode);

        var beforeHints = await CountUserHintsAsync(userId);
        var beforeCoins = await GetCoinsAsync(userId);

        var response = await GetAsUserAsync(userId, $"/api/hints/questions/{questionId}/formula");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Use the distributive property.", payload.GetProperty("formula").GetString());
        Assert.True(payload.GetProperty("alreadyUsed").GetBoolean());
        Assert.Equal(beforeHints, await CountUserHintsAsync(userId));
        Assert.Equal(beforeCoins, await GetCoinsAsync(userId));
    }

    [Fact]
    public async Task LegacyQuestionHintAlias_IsGone_AndCannotBypassPaidContent()
    {
        var userId = NewUserId("legacy-hint");
        await EnsureUserAsync(userId, coins: 100);
        var questionId = await EnsureQuestionWithHintsAsync();

        var response = await GetAsUserAsync(userId, $"/api/questions/{questionId}/hint/formula");
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task CoinsHistory_UsesCanonicalEconomyTransactions()
    {
        var userId = NewUserId("history");
        await EnsureUserAsync(userId, coins: 100);

        var spend = await PostAsUserAsync(userId, "/api/economy/coins/spend", new
        {
            operationId = "history-op",
            idempotencyKey = "history-key",
            amount = 10,
            reason = "hint"
        });
        Assert.Equal(HttpStatusCode.OK, spend.StatusCode);

        var response = await GetAsUserAsync(userId, "/api/coins/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var first = payload.EnumerateArray().First();
        Assert.Equal("economy_coins_spend", first.GetProperty("transactionType").GetString());
        Assert.Equal(-10, first.GetProperty("amount").GetInt32());
    }

    private static string NewUserId(string suffix) => $"legacy-economy-{suffix}-{Guid.NewGuid():N}";

    private async Task<HttpResponseMessage> PostAsUserAsync(string userId, string url, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetAsUserAsync(string userId, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Test-UserId", userId);
        return await client.SendAsync(request);
    }

    private async Task EnsureUserAsync(string userId, int coins, int streakFreezes = 0)
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
            profile = new UserProfile { UserId = userId };
            db.UserProfiles.Add(profile);
        }

        profile.Coins = coins;
        profile.StreakFreezeCount = streakFreezes;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<int> EnsureQuestionWithHintsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var existing = await db.Questions
            .Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.HintFormula == "Use the distributive property.");
        if (existing is not null)
            return existing.Id;

        var categoryId = await db.Questions.Select(q => q.CategoryId).FirstAsync();
        var subtopicId = await db.Questions.Select(q => q.SubtopicId).FirstAsync();

        var question = new Question("What is 2 + 2?", 1, categoryId, "Because 2 + 2 = 4.");
        question.SetSubtopic(subtopicId);
        question.SetType("multiple_choice");
        question.SetHintFormula("Use the distributive property.");
        question.SetHintClue("Think about adding two pairs.");
        question.ReplaceOptions(
        [
            new QuestionOption("3", false, order: 1),
            new QuestionOption("4", true, order: 2),
            new QuestionOption("5", false, order: 3)
        ]);
        question.SyncCorrectOptionFromOptions();

        db.Questions.Add(question);
        await db.SaveChangesAsync();
        return question.Id;
    }

    private async Task<int> CountUserHintsAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserHints.CountAsync(x => x.UserId == userId);
    }

    private async Task<int> GetCoinsAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserProfiles.Where(x => x.UserId == userId).Select(x => x.Coins).SingleAsync();
    }

    private async Task<int> GetStreakFreezeCountAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.UserProfiles.Where(x => x.UserId == userId).Select(x => x.StreakFreezeCount).SingleAsync();
    }
}

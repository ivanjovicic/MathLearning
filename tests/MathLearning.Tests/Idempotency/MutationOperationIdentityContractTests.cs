using System.Net;
using System.Text;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Api.Endpoints;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Idempotency;

public sealed class MutationOperationIdentityContractTests :
    IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public MutationOperationIdentityContractTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public void QuizKeys_OperationIdOnly_UsesSameValueForBothKeys()
    {
        using var document = JsonDocument.Parse("""{"operationId":"  quiz-op-1  "}""");

        var resolved = QuizEndpointHelpers.TryResolveQuizAnswerKeys(
            document.RootElement,
            out var operationId,
            out var idempotencyKey);

        Assert.True(resolved);
        Assert.Equal("quiz-op-1", operationId);
        Assert.Equal("quiz-op-1", idempotencyKey);
    }

    [Fact]
    public void QuizKeys_IdempotencyKeyOnly_UsesSameValueForBothKeys()
    {
        using var document = JsonDocument.Parse("""{"idempotencyKey":"  quiz-key-1  "}""");

        var resolved = QuizEndpointHelpers.TryResolveQuizAnswerKeys(
            document.RootElement,
            out var operationId,
            out var idempotencyKey);

        Assert.True(resolved);
        Assert.Equal("quiz-key-1", operationId);
        Assert.Equal("quiz-key-1", idempotencyKey);
    }

    [Fact]
    public void QuizKeys_DistinctValues_ArePreservedAndTrimmed()
    {
        using var document = JsonDocument.Parse(
            """{"operationId":" quiz-op-2 ","idempotencyKey":" quiz-key-2 "}""");

        var resolved = QuizEndpointHelpers.TryResolveQuizAnswerKeys(
            document.RootElement,
            out var operationId,
            out var idempotencyKey);

        Assert.True(resolved);
        Assert.Equal("quiz-op-2", operationId);
        Assert.Equal("quiz-key-2", idempotencyKey);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"operationId\":\"   \"}")]
    [InlineData("{\"operationId\":123,\"idempotencyKey\":false}")]
    public void QuizKeys_MissingBlankOrNonStringIdentity_UsesLegacyMode(string json)
    {
        using var document = JsonDocument.Parse(json);

        var resolved = QuizEndpointHelpers.TryResolveQuizAnswerKeys(
            document.RootElement,
            out var operationId,
            out var idempotencyKey);

        Assert.False(resolved);
        Assert.Equal(string.Empty, operationId);
        Assert.Equal(string.Empty, idempotencyKey);
    }

    [Fact]
    public void SrsKeys_OperationIdOnly_UsesSameValueForBothKeys()
    {
        var resolved = SrsEndpointHelpers.TryResolveSrsUpdateKeys(
            "  srs-op-1  ",
            null,
            out var operationId,
            out var idempotencyKey);

        Assert.True(resolved);
        Assert.Equal("srs-op-1", operationId);
        Assert.Equal("srs-op-1", idempotencyKey);
    }

    [Fact]
    public void SrsKeys_IdempotencyKeyOnly_UsesSameValueForBothKeys()
    {
        var resolved = SrsEndpointHelpers.TryResolveSrsUpdateKeys(
            null,
            "  srs-key-1  ",
            out var operationId,
            out var idempotencyKey);

        Assert.True(resolved);
        Assert.Equal("srs-key-1", operationId);
        Assert.Equal("srs-key-1", idempotencyKey);
    }

    [Fact]
    public void SrsKeys_DistinctValues_ArePreservedAndTrimmed()
    {
        var resolved = SrsEndpointHelpers.TryResolveSrsUpdateKeys(
            "  srs-op-2  ",
            "  srs-key-2  ",
            out var operationId,
            out var idempotencyKey);

        Assert.True(resolved);
        Assert.Equal("srs-op-2", operationId);
        Assert.Equal("srs-key-2", idempotencyKey);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData(null, "   ")]
    [InlineData("  ", "  ")]
    public void SrsKeys_MissingOrBlankIdentity_UsesLegacyMode(
        string? rawOperationId,
        string? rawIdempotencyKey)
    {
        var resolved = SrsEndpointHelpers.TryResolveSrsUpdateKeys(
            rawOperationId,
            rawIdempotencyKey,
            out var operationId,
            out var idempotencyKey);

        Assert.False(resolved);
        Assert.Equal(string.Empty, operationId);
        Assert.Equal(string.Empty, idempotencyKey);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task QuizAnswer_SingleIdentityField_ReplaysWithoutDoubleApply(
        bool sendOperationId,
        bool sendIdempotencyKey)
    {
        var userId = NewUserId("quiz-single-identity");
        await EnsureUserAsync(userId);
        var identity = $"quiz-single-{Guid.NewGuid():N}";
        var quizId = Guid.NewGuid().ToString();
        var payload = BuildQuizPayload(
            quizId,
            operationId: sendOperationId ? identity : null,
            idempotencyKey: sendIdempotencyKey ? identity : null);

        var first = await PostAsync(userId, "/api/quiz/answer", payload);
        var replay = await PostAsync(userId, "/api/quiz/answer", payload);

        await AssertOkAsync(first);
        await AssertOkAsync(replay);
        var replayJson = await ReadJsonAsync(replay);
        Assert.True(replayJson.GetProperty("alreadyProcessed").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var ledger = await db.IdempotencyLedgers.SingleAsync(x =>
            x.UserId == userId && x.OperationType == QuizOperationTypes.QuizAnswer);
        Assert.Equal(identity, ledger.OperationId);
        Assert.Equal(identity, ledger.IdempotencyKey);
        Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == userId && x.QuestionId == 1));
        Assert.Equal(1, (await db.UserQuestionStats.SingleAsync(x =>
            x.UserId == userId && x.QuestionId == 1)).Attempts);
    }

    [Fact]
    public async Task QuizAnswer_NoIdentity_RemainsLegacyCompatible_WithoutLedger()
    {
        var userId = NewUserId("quiz-legacy");
        await EnsureUserAsync(userId);

        var response = await PostAsync(
            userId,
            "/api/quiz/answer",
            BuildQuizPayload(Guid.NewGuid().ToString(), operationId: null, idempotencyKey: null));

        await AssertOkAsync(response);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.False(await db.IdempotencyLedgers.AnyAsync(x =>
            x.UserId == userId && x.OperationType == QuizOperationTypes.QuizAnswer));
        Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == userId && x.QuestionId == 1));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task SrsUpdate_SingleIdentityField_ReplaysWithoutDoubleApply(
        bool sendOperationId,
        bool sendIdempotencyKey)
    {
        var userId = NewUserId("srs-single-identity");
        await EnsureUserAsync(userId);
        var identity = $"srs-single-{Guid.NewGuid():N}";
        var payload = BuildSrsPayload(
            operationId: sendOperationId ? identity : null,
            idempotencyKey: sendIdempotencyKey ? identity : null);

        var first = await PostAsync(userId, "/api/quiz/srs/update", payload);
        await AssertOkAsync(first);
        var firstState = await GetQuestionStatAsync(userId);

        var replay = await PostAsync(userId, "/api/quiz/srs/update", payload);
        await AssertOkAsync(replay);
        var replayJson = await ReadJsonAsync(replay);
        Assert.True(replayJson.GetProperty("alreadyProcessed").GetBoolean());
        var replayState = await GetQuestionStatAsync(userId);

        Assert.Equal(firstState.SuccessStreak, replayState.SuccessStreak);
        Assert.Equal(firstState.NextReview, replayState.NextReview);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var ledger = await db.IdempotencyLedgers.SingleAsync(x =>
            x.UserId == userId && x.OperationType == QuizOperationTypes.SrsUpdate);
        Assert.Equal(identity, ledger.OperationId);
        Assert.Equal(identity, ledger.IdempotencyKey);
    }

    [Fact]
    public async Task SrsUpdate_NoIdentity_RemainsLegacyCompatible_WithoutLedger()
    {
        var userId = NewUserId("srs-legacy");
        await EnsureUserAsync(userId);

        var response = await PostAsync(
            userId,
            "/api/quiz/srs/update",
            BuildSrsPayload(operationId: null, idempotencyKey: null));

        await AssertOkAsync(response);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.False(await db.IdempotencyLedgers.AnyAsync(x =>
            x.UserId == userId && x.OperationType == QuizOperationTypes.SrsUpdate));
        Assert.Equal(1, (await db.QuestionStats.SingleAsync(x =>
            x.UserId == userId && x.QuestionId == 1)).SuccessStreak);
    }

    private static Dictionary<string, object?> BuildQuizPayload(
        string quizId,
        string? operationId,
        string? idempotencyKey)
        => new()
        {
            ["quizId"] = quizId,
            ["questionId"] = 1,
            ["answer"] = "2",
            ["timeSpentSeconds"] = 5,
            ["operationId"] = operationId,
            ["idempotencyKey"] = idempotencyKey
        };

    private static Dictionary<string, object?> BuildSrsPayload(
        string? operationId,
        string? idempotencyKey)
        => new()
        {
            ["questionId"] = 1,
            ["isCorrect"] = true,
            ["timeMs"] = 1200,
            ["operationId"] = operationId,
            ["idempotencyKey"] = idempotencyKey
        };

    private async Task EnsureUserAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (await userManager.FindByIdAsync(userId) is null)
        {
            await userManager.CreateAsync(new IdentityUser
            {
                Id = userId,
                UserName = userId,
                Email = $"{userId}@test.local"
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
            await db.SaveChangesAsync();
        }
    }

    private async Task<QuestionStat> GetQuestionStatAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.QuestionStats.AsNoTracking().SingleAsync(x =>
            x.UserId == userId && x.QuestionId == 1);
    }

    private async Task<HttpResponseMessage> PostAsync(string userId, string url, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await client.SendAsync(request);
    }

    private static async Task AssertOkAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected OK but got {response.StatusCode}: {body}");
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static string NewUserId(string prefix)
        => $"mutation-identity-{prefix}-{Guid.NewGuid():N}";
}

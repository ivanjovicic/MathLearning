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

public sealed class MobileMutationContractIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public MobileMutationContractIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task QuizAnswer_ValidRequest_ReplayAndConflictMatchMobileContract()
    {
        var userId = NewUserId("quiz-answer");
        await EnsureUserAsync(userId, coins: 0, xp: 0);
        var issuedQuiz = await StartIssuedQuizAsync(userId);

        const string operationId = "mobile-quiz-answer-op";
        const string idempotencyKey = "mobile-quiz-answer-key";
        var payload = new
        {
            quizId = issuedQuiz.QuizId.ToString(),
            questionId = issuedQuiz.QuestionId,
            answer = "2",
            timeSpentSeconds = 5,
            operationId,
            idempotencyKey
        };

        var first = await PostAsUserAsync(userId, "/api/quiz/answer", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstJson = await ReadJsonAsync(first);
        Assert.True(firstJson.TryGetProperty("isCorrect", out _));

        var replay = await PostAsUserAsync(userId, "/api/quiz/answer", payload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayJson = await ReadJsonAsync(replay);
        Assert.True(replayJson.GetProperty("alreadyProcessed").GetBoolean());

        var conflict = await PostAsUserAsync(userId, "/api/quiz/answer", new
        {
            quizId = payload.quizId,
            questionId = issuedQuiz.QuestionId,
            answer = "3",
            timeSpentSeconds = 5,
            operationId,
            idempotencyKey
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await AssertBusinessErrorCodeAsync(conflict, "idempotency_conflict");
    }

    [Fact]
    public async Task QuizAnswer_RejectsQuestionNotIssuedInSession()
    {
        var userId = NewUserId("quiz-answer-question");
        await EnsureUserAsync(userId, coins: 0, xp: 0);
        var issuedQuiz = await StartIssuedQuizAsync(userId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var foreignQuestionId = await db.Questions
            .Where(q => q.Id != issuedQuiz.QuestionId)
            .Select(q => q.Id)
            .FirstAsync();

        var response = await PostAsUserAsync(userId, "/api/quiz/answer", new
        {
            quizId = issuedQuiz.QuizId.ToString(),
            questionId = foreignQuestionId,
            answer = "2",
            timeSpentSeconds = 5
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task QuizAnswer_RejectsSessionOwnedByDifferentUser()
    {
        var userA = NewUserId("quiz-answer-owner-a");
        var userB = NewUserId("quiz-answer-owner-b");
        await EnsureUserAsync(userA, coins: 0, xp: 0);
        await EnsureUserAsync(userB, coins: 0, xp: 0);

        var issuedQuiz = await StartIssuedQuizAsync(userA);

        var response = await PostAsUserAsync(userB, "/api/quiz/answer", new
        {
            quizId = issuedQuiz.QuizId.ToString(),
            questionId = issuedQuiz.QuestionId,
            answer = "2",
            timeSpentSeconds = 5
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SrsUpdate_ValidRequest_ReplayAndConflictMatchMobileContract()
    {
        var userId = NewUserId("srs-update");
        await EnsureUserAsync(userId, coins: 0, xp: 0);

        const string operationId = "mobile-srs-op";
        const string idempotencyKey = "mobile-srs-key";
        var payload = new
        {
            questionId = 1,
            isCorrect = true,
            timeMs = 1200,
            operationId,
            idempotencyKey
        };

        var first = await PostAsUserAsync(userId, "/api/quiz/srs/update", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstJson = await ReadJsonAsync(first);
        Assert.True(firstJson.TryGetProperty("nextReview", out _));
        Assert.True(firstJson.TryGetProperty("streak", out _));

        var replay = await PostAsUserAsync(userId, "/api/quiz/srs/update", payload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayJson = await ReadJsonAsync(replay);
        Assert.True(replayJson.GetProperty("alreadyProcessed").GetBoolean());

        var conflict = await PostAsUserAsync(userId, "/api/quiz/srs/update", new
        {
            questionId = 1,
            isCorrect = false,
            timeMs = 1200,
            operationId,
            idempotencyKey
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await AssertBusinessErrorCodeAsync(conflict, "idempotency_conflict");
    }

    [Fact]
    public async Task DailyRunChestClaim_ValidRequest_ReplaysSettledResult()
    {
        var userId = NewUserId("daily-run");
        var day = new DateOnly(2026, 06, 24);
        await EnsureUserAsync(userId, coins: 0, xp: 0);
        await EnsureDailyRunCompletedAsync(userId, day);

        var payload = new
        {
            transactionId = "mobile-daily-run-tx-1",
            date = "2026-06-24"
        };

        var first = await PostAsUserAsync(userId, "/api/daily-run/chest/claim", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstJson = await ReadJsonAsync(first);
        Assert.True(firstJson.GetProperty("success").GetBoolean());
        Assert.False(firstJson.GetProperty("alreadyClaimed").GetBoolean());
        Assert.True(firstJson.GetProperty("reward").GetProperty("xp").GetInt32() > 0);

        var replay = await PostAsUserAsync(userId, "/api/daily-run/chest/claim", payload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayJson = await ReadJsonAsync(replay);
        Assert.True(replayJson.GetProperty("alreadyClaimed").GetBoolean());
        Assert.Equal(firstJson.GetProperty("transactionId").GetString(), replayJson.GetProperty("transactionId").GetString());
        Assert.Equal(firstJson.GetProperty("reward").ToString(), replayJson.GetProperty("reward").ToString());
    }

    [Fact]
    public async Task CoinSpend_ValidRequest_ReplayAndConflictMatchMobileContract()
    {
        var userId = NewUserId("coins");
        await EnsureUserAsync(userId, coins: 100, xp: 0);

        const string operationId = "mobile-coins-op";
        const string idempotencyKey = "mobile-coins-key";
        var payload = new
        {
            operationId,
            idempotencyKey,
            amount = 10,
            reason = "hint"
        };

        var first = await PostAsUserAsync(userId, "/api/economy/coins/spend", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstJson = await ReadJsonAsync(first);
        Assert.True(firstJson.GetProperty("success").GetBoolean());
        Assert.Equal(90, firstJson.GetProperty("coins").GetInt32());

        var replay = await PostAsUserAsync(userId, "/api/economy/coins/spend", payload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayJson = await ReadJsonAsync(replay);
        Assert.True(replayJson.GetProperty("alreadyProcessed").GetBoolean());

        var conflict = await PostAsUserAsync(userId, "/api/economy/coins/spend", new
        {
            operationId,
            idempotencyKey,
            amount = 20,
            reason = "hint"
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await AssertBusinessErrorCodeAsync(conflict, "idempotency_conflict");
    }

    [Fact]
    public async Task HintUse_ValidRequest_ReplayAndConflictMatchMobileContract()
    {
        var userId = NewUserId("hint");
        await EnsureUserAsync(userId, coins: 100, xp: 0);

        const string operationId = "mobile-hint-op";
        const string idempotencyKey = "mobile-hint-key";
        var payload = new
        {
            operationId,
            idempotencyKey,
            questionId = 42,
            hintType = "clue",
            costCoins = 10
        };

        var first = await PostAsUserAsync(userId, "/api/economy/hints/use", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstJson = await ReadJsonAsync(first);
        Assert.True(firstJson.GetProperty("success").GetBoolean());
        Assert.True(firstJson.TryGetProperty("usedFreeHint", out _));

        var replay = await PostAsUserAsync(userId, "/api/economy/hints/use", payload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayJson = await ReadJsonAsync(replay);
        Assert.True(replayJson.GetProperty("alreadyProcessed").GetBoolean());

        var conflict = await PostAsUserAsync(userId, "/api/economy/hints/use", new
        {
            operationId,
            idempotencyKey,
            questionId = 99,
            hintType = "clue",
            costCoins = 10
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await AssertBusinessErrorCodeAsync(conflict, "idempotency_conflict");
    }

    [Fact]
    public async Task StreakFreezePurchase_ValidRequest_ReplayAndConflictMatchMobileContract()
    {
        var userId = NewUserId("freeze");
        await EnsureUserAsync(userId, coins: 200, xp: 0, streakFreezes: 0);

        const string operationId = "mobile-freeze-op";
        const string idempotencyKey = "mobile-freeze-key";
        var payload = new
        {
            operationId,
            idempotencyKey,
            quantity = 1
        };

        var first = await PostAsUserAsync(userId, "/api/shop/streak-freeze/purchase", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstJson = await ReadJsonAsync(first);
        Assert.True(firstJson.GetProperty("success").GetBoolean());
        Assert.Equal(1, firstJson.GetProperty("streakFreezeCount").GetInt32());

        var replay = await PostAsUserAsync(userId, "/api/shop/streak-freeze/purchase", payload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayJson = await ReadJsonAsync(replay);
        Assert.True(replayJson.GetProperty("alreadyProcessed").GetBoolean());

        var conflict = await PostAsUserAsync(userId, "/api/shop/streak-freeze/purchase", new
        {
            operationId,
            idempotencyKey,
            quantity = 2
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await AssertBusinessErrorCodeAsync(conflict, "idempotency_conflict");
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

    private static string NewUserId(string suffix) => $"mobile-http-{suffix}-{Guid.NewGuid():N}";

    private async Task EnsureUserAsync(string userId, int coins, int xp, int streakFreezes = 0)
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

    private async Task<(Guid QuizId, int QuestionId)> StartIssuedQuizAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var subtopicId = await db.Questions.Select(q => q.SubtopicId).FirstAsync();

        var response = await PostAsUserAsync(userId, "/api/quiz/start", new
        {
            subtopicId,
            questionCount = 1
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        var quizId = Guid.Parse(payload.GetProperty("quizId").GetString()!);
        var questionId = payload.GetProperty("questions").EnumerateArray().Single().GetProperty("id").GetInt32();
        return (quizId, questionId);
    }

    private async Task EnsureDailyRunCompletedAsync(string userId, DateOnly day)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var stat = await db.UserDailyStats.FirstOrDefaultAsync(x => x.UserId == userId && x.Day == day);
        if (stat is null)
        {
            stat = new UserDailyStat
            {
                UserId = userId,
                Day = day
            };
            db.UserDailyStats.Add(stat);
        }

        stat.Completed = true;
        await db.SaveChangesAsync();
    }
}

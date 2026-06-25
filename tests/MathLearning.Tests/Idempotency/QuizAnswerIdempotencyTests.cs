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

namespace MathLearning.Tests.Idempotency;

public sealed class QuizAnswerIdempotencyTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public QuizAnswerIdempotencyTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FirstSuccess_AppliesAnswerOnce()
    {
        var userId = NewUserId("first-success");
        await EnsureUserAsync(userId);
        var quizId = Guid.NewGuid().ToString();
        const string operationId = "quiz-answer-first";
        const string idempotencyKey = "quiz-answer-first-key";

        var response = await PostQuizAnswerAsync(
            userId,
            BuildPayload(
                quizId: quizId,
                questionId: 1,
                answer: "2",
                timeSpentSeconds: 5,
                operationId: operationId,
                idempotencyKey: idempotencyKey));

        await AssertOkAsync(response);
        var payload = await ReadJsonAsync(response);
        Assert.True(payload.GetProperty("isCorrect").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var stat = await db.UserQuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == 1);
        Assert.Equal(1, stat.Attempts);
        Assert.Equal(1, stat.CorrectAttempts);

        var ledger = await db.IdempotencyLedgers.SingleAsync(
            x => x.UserId == userId && x.OperationType == QuizOperationTypes.QuizAnswer);
        Assert.Equal(IdempotencyLedgerStatuses.Completed, ledger.Status);
        Assert.Equal(operationId, ledger.OperationId);
    }

    [Fact]
    public async Task DuplicateSamePayload_ReturnsSettledSuccess_WithoutDoubleApply()
    {
        var userId = NewUserId("duplicate");
        await EnsureUserAsync(userId);
        var quizId = Guid.NewGuid().ToString();
        const string operationId = "quiz-answer-dup";
        const string idempotencyKey = "quiz-answer-dup-key";
        var body = BuildPayload(
            quizId: quizId,
            questionId: 1,
            answer: "2",
            timeSpentSeconds: 5,
            operationId: operationId,
            idempotencyKey: idempotencyKey);

        var first = await PostQuizAnswerAsync(userId, body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await PostQuizAnswerAsync(userId, body);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayPayload = await ReadJsonAsync(replay);
        Assert.True(replayPayload.GetProperty("alreadyProcessed").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var stat = await db.UserQuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == 1);
        Assert.Equal(1, stat.Attempts);
        Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == userId && x.QuestionId == 1));
    }

    [Fact]
    public async Task SameKeysDifferentAnswer_ReturnsIdempotencyConflict()
    {
        var userId = NewUserId("conflict");
        await EnsureUserAsync(userId);
        var quizId = Guid.NewGuid().ToString();
        const string operationId = "quiz-answer-conflict";
        const string idempotencyKey = "quiz-answer-conflict-key";

        var first = await PostQuizAnswerAsync(
            userId,
            BuildPayload(
                quizId: quizId,
                questionId: 1,
                answer: "2",
                timeSpentSeconds: 5,
                operationId: operationId,
                idempotencyKey: idempotencyKey));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var conflict = await PostQuizAnswerAsync(
            userId,
            BuildPayload(
                quizId: quizId,
                questionId: 1,
                answer: "3",
                timeSpentSeconds: 5,
                operationId: operationId,
                idempotencyKey: idempotencyKey));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var conflictPayload = await ReadJsonAsync(conflict);
        Assert.Equal("idempotency_conflict", conflictPayload.GetProperty("errorCode").GetString());
        Assert.True(conflictPayload.GetProperty("conflict").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var stat = await db.UserQuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == 1);
        Assert.Equal(1, stat.Attempts);
    }

    [Fact]
    public async Task FailedDomainMutation_DoesNotLeaveCompletedLedgerRow()
    {
        var userId = NewUserId("rollback");
        await EnsureUserWithoutProfileAsync(userId);
        var quizId = Guid.NewGuid().ToString();
        const string operationId = "quiz-answer-rollback";
        const string idempotencyKey = "quiz-answer-rollback-key";

        var response = await PostQuizAnswerAsync(
            userId,
            BuildPayload(
                quizId: quizId,
                questionId: 1,
                answer: "2",
                timeSpentSeconds: 5,
                operationId: operationId,
                idempotencyKey: idempotencyKey));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.False(await db.IdempotencyLedgers.AnyAsync(
            x => x.UserId == userId &&
                 x.OperationType == QuizOperationTypes.QuizAnswer &&
                 x.Status == IdempotencyLedgerStatuses.Completed));
        Assert.False(await db.UserAnswers.AnyAsync(x => x.UserId == userId));
    }

    [Fact]
    public async Task SameOperationKeys_DifferentUsers_AreIsolated()
    {
        var userA = NewUserId("user-a");
        var userB = NewUserId("user-b");
        await EnsureUserAsync(userA);
        await EnsureUserAsync(userB);
        var quizIdA = Guid.NewGuid().ToString();
        var quizIdB = Guid.NewGuid().ToString();
        const string operationId = "shared-operation-id";
        const string idempotencyKey = "shared-idempotency-key";

        var payloadA = BuildPayload(
            quizId: quizIdA,
            questionId: 1,
            answer: "2",
            timeSpentSeconds: 4,
            operationId: operationId,
            idempotencyKey: idempotencyKey);
        var payloadB = BuildPayload(
            quizId: quizIdB,
            questionId: 1,
            answer: "2",
            timeSpentSeconds: 4,
            operationId: operationId,
            idempotencyKey: idempotencyKey);

        var firstA = await PostQuizAnswerAsync(userA, payloadA);
        var firstB = await PostQuizAnswerAsync(userB, payloadB);
        await AssertOkAsync(firstA);
        await AssertOkAsync(firstB);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(2, await db.IdempotencyLedgers.CountAsync(
            x => x.OperationId == operationId && x.OperationType == QuizOperationTypes.QuizAnswer));
        Assert.Equal(2, await db.UserQuestionStats.CountAsync(
            x => (x.UserId == userA || x.UserId == userB) && x.QuestionId == 1 && x.Attempts == 1));
    }

    private static object BuildPayload(
        string quizId,
        int questionId,
        string answer,
        int timeSpentSeconds,
        string operationId,
        string idempotencyKey)
    {
        return new
        {
            quizId,
            questionId,
            answer,
            timeSpentSeconds,
            operationId,
            idempotencyKey
        };
    }

    private async Task<HttpResponseMessage> PostQuizAnswerAsync(string userId, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/quiz/answer")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await _client.SendAsync(request);
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
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static string NewUserId(string prefix) => $"quiz-idem-{prefix}-{Guid.NewGuid():N}";

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

    private async Task EnsureUserWithoutProfileAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        if (await userManager.FindByIdAsync(userId) is null)
        {
            await userManager.CreateAsync(new IdentityUser { Id = userId, UserName = userId });
        }
    }
}

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
        var issuedQuiz = await StartIssuedQuizAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync(issuedQuiz.QuestionId);
        const string operationId = "quiz-answer-first";
        const string idempotencyKey = "quiz-answer-first-key";

        var response = await PostQuizAnswerAsync(
            userId,
            BuildPayload(
                quizId: issuedQuiz.QuizId.ToString(),
                questionId: issuedQuiz.QuestionId,
                answer: correctAnswer,
                timeSpentSeconds: 5,
                operationId: operationId,
                idempotencyKey: idempotencyKey));

        await AssertOkAsync(response);
        var payload = await ReadJsonAsync(response);
        Assert.True(payload.GetProperty("isCorrect").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var stat = await db.UserQuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == issuedQuiz.QuestionId);
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
        var issuedQuiz = await StartIssuedQuizAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync(issuedQuiz.QuestionId);
        const string operationId = "quiz-answer-dup";
        const string idempotencyKey = "quiz-answer-dup-key";
        var body = BuildPayload(
            quizId: issuedQuiz.QuizId.ToString(),
            questionId: issuedQuiz.QuestionId,
            answer: correctAnswer,
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
        var stat = await db.UserQuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == issuedQuiz.QuestionId);
        Assert.Equal(1, stat.Attempts);
        Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == userId && x.QuestionId == issuedQuiz.QuestionId));
    }

    [Fact]
    public async Task SameKeysDifferentAnswer_ReturnsIdempotencyConflict()
    {
        var userId = NewUserId("conflict");
        await EnsureUserAsync(userId);
        var issuedQuiz = await StartIssuedQuizAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync(issuedQuiz.QuestionId);
        const string operationId = "quiz-answer-conflict";
        const string idempotencyKey = "quiz-answer-conflict-key";

        var first = await PostQuizAnswerAsync(
            userId,
            BuildPayload(
                quizId: issuedQuiz.QuizId.ToString(),
                questionId: issuedQuiz.QuestionId,
                answer: correctAnswer,
                timeSpentSeconds: 5,
                operationId: operationId,
                idempotencyKey: idempotencyKey));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var conflict = await PostQuizAnswerAsync(
            userId,
            BuildPayload(
                quizId: issuedQuiz.QuizId.ToString(),
                questionId: issuedQuiz.QuestionId,
                answer: "wrong-answer",
                timeSpentSeconds: 5,
                operationId: operationId,
                idempotencyKey: idempotencyKey));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var conflictPayload = await ReadJsonAsync(conflict);
        Assert.Equal("idempotency_conflict", conflictPayload.GetProperty("errorCode").GetString());
        Assert.True(conflictPayload.GetProperty("conflict").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var stat = await db.UserQuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == issuedQuiz.QuestionId);
        Assert.Equal(1, stat.Attempts);
    }

    [Fact]
    public async Task FailedDomainMutation_DoesNotLeaveCompletedLedgerRow()
    {
        var userId = NewUserId("rollback");
        await EnsureUserWithoutProfileAsync(userId);
        var issuedQuiz = await StartIssuedQuizAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync(issuedQuiz.QuestionId);
        const string operationId = "quiz-answer-rollback";
        const string idempotencyKey = "quiz-answer-rollback-key";

        var response = await PostQuizAnswerAsync(
            userId,
            BuildPayload(
                quizId: issuedQuiz.QuizId.ToString(),
                questionId: issuedQuiz.QuestionId,
                answer: correctAnswer,
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
        var quizA = await StartIssuedQuizAsync(userA);
        var quizB = await StartIssuedQuizAsync(userB);
        var answerA = await GetCorrectAnswerTokenAsync(quizA.QuestionId);
        var answerB = await GetCorrectAnswerTokenAsync(quizB.QuestionId);
        const string operationId = "shared-operation-id";
        const string idempotencyKey = "shared-idempotency-key";

        var payloadA = BuildPayload(
            quizId: quizA.QuizId.ToString(),
            questionId: quizA.QuestionId,
            answer: answerA,
            timeSpentSeconds: 4,
            operationId: operationId,
            idempotencyKey: idempotencyKey);
        var payloadB = BuildPayload(
            quizId: quizB.QuizId.ToString(),
            questionId: quizB.QuestionId,
            answer: answerB,
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
            x => (x.UserId == userA || x.UserId == userB) && (x.QuestionId == quizA.QuestionId || x.QuestionId == quizB.QuestionId) && x.Attempts == 1));
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
        => await PostAsUserAsync(userId, "/api/quiz/answer", payload);

    private async Task<HttpResponseMessage> PostAsUserAsync(string userId, string url, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
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

    private async Task<string> GetCorrectAnswerTokenAsync(int questionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var question = await db.Questions
            .Include(q => q.Options)
            .SingleAsync(q => q.Id == questionId);
        var correctOption = question.Options.First(o => o.IsCorrect);

        return correctOption.Id > 0
            ? correctOption.Id.ToString()
            : question.CorrectAnswer ?? throw new InvalidOperationException($"Question {questionId} has no correct answer");
    }

    private async Task<(Guid QuizId, int QuestionId)> StartIssuedQuizAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var subtopicId = await db.Questions.Select(q => q.SubtopicId).FirstAsync();

        var response = await PostAsUserAsync(
            userId,
            "/api/quiz/start",
            new
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
}

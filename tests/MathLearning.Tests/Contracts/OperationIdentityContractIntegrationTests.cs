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

public sealed class OperationIdentityContractIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public OperationIdentityContractIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("operationId")]
    [InlineData("idempotencyKey")]
    public async Task QuizAnswer_SingleIdentityField_IsPromotedToBothLedgerKeys_AndReplays(string identityField)
    {
        var userId = NewUserId($"quiz-{identityField}");
        await EnsureUserAsync(userId);
        var issuedQuiz = await StartIssuedQuizAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync(issuedQuiz.QuestionId);
        var identity = $"quiz-single-{identityField}-{Guid.NewGuid():N}";
        var payload = BuildQuizAnswerPayload(identityField, identity, issuedQuiz.QuizId, issuedQuiz.QuestionId, correctAnswer);

        var first = await PostAsUserAsync(userId, "/api/quiz/answer", payload);
        await AssertOkAsync(first);

        var replay = await PostAsUserAsync(userId, "/api/quiz/answer", payload);
        await AssertOkAsync(replay);
        var replayJson = await ReadJsonAsync(replay);
        Assert.True(replayJson.GetProperty("alreadyProcessed").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var ledger = await db.IdempotencyLedgers.SingleAsync(x =>
            x.UserId == userId && x.OperationType == QuizOperationTypes.QuizAnswer);

        Assert.Equal(identity, ledger.OperationId);
        Assert.Equal(identity, ledger.IdempotencyKey);
        Assert.Equal(IdempotencyLedgerStatuses.Completed, ledger.Status);
        Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == userId && x.QuestionId == issuedQuiz.QuestionId));

        var stat = await db.UserQuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == issuedQuiz.QuestionId);
        Assert.Equal(1, stat.Attempts);
    }

    [Fact]
    public async Task QuizAnswer_MissingOperationIdentity_UsesLegacyPathWithoutLedger()
    {
        var userId = NewUserId("quiz-missing");
        await EnsureUserAsync(userId);
        var issuedQuiz = await StartIssuedQuizAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync(issuedQuiz.QuestionId);
        var payload = BuildQuizAnswerPayload(null, null, issuedQuiz.QuizId, issuedQuiz.QuestionId, correctAnswer);

        var response = await PostAsUserAsync(userId, "/api/quiz/answer", payload);
        await AssertOkAsync(response);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.False(await db.IdempotencyLedgers.AnyAsync(x =>
            x.UserId == userId && x.OperationType == QuizOperationTypes.QuizAnswer));
        Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == userId && x.QuestionId == issuedQuiz.QuestionId));
    }

    [Theory]
    [InlineData("operationId")]
    [InlineData("idempotencyKey")]
    public async Task SrsUpdate_SingleIdentityField_IsPromotedToBothLedgerKeys_AndReplays(string identityField)
    {
        var userId = NewUserId($"srs-{identityField}");
        await EnsureUserAsync(userId);
        var identity = $"srs-single-{identityField}-{Guid.NewGuid():N}";
        var payload = BuildSrsPayload(identityField, identity);

        var first = await PostAsUserAsync(userId, "/api/quiz/srs/update", payload);
        await AssertOkAsync(first);

        var replay = await PostAsUserAsync(userId, "/api/quiz/srs/update", payload);
        await AssertOkAsync(replay);
        var replayJson = await ReadJsonAsync(replay);
        Assert.True(replayJson.GetProperty("alreadyProcessed").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var ledger = await db.IdempotencyLedgers.SingleAsync(x =>
            x.UserId == userId && x.OperationType == QuizOperationTypes.SrsUpdate);

        Assert.Equal(identity, ledger.OperationId);
        Assert.Equal(identity, ledger.IdempotencyKey);
        Assert.Equal(IdempotencyLedgerStatuses.Completed, ledger.Status);

        var stat = await db.QuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == 1);
        Assert.Equal(1, stat.SuccessStreak);
    }

    [Fact]
    public async Task SrsUpdate_MissingOperationIdentity_UsesLegacyPathWithoutLedger()
    {
        var userId = NewUserId("srs-missing");
        await EnsureUserAsync(userId);
        var payload = BuildSrsPayload(null, null);

        var response = await PostAsUserAsync(userId, "/api/quiz/srs/update", payload);
        await AssertOkAsync(response);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.False(await db.IdempotencyLedgers.AnyAsync(x =>
            x.UserId == userId && x.OperationType == QuizOperationTypes.SrsUpdate));

        var stat = await db.QuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == 1);
        Assert.Equal(1, stat.SuccessStreak);
    }

    [Fact]
    public async Task OfflineSubmit_MissingSessionIdentity_DeduplicatesAnswerButCreatesIndependentSessions()
    {
        var userId = NewUserId("offline-missing-session");
        await EnsureUserAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync();
        var answeredAt = DateTime.UtcNow.AddMinutes(-10);
        var payload = new
        {
            sessionId = string.Empty,
            answers = new[]
            {
                new
                {
                    questionId = 1,
                    answer = correctAnswer,
                    isCorrectOffline = false,
                    timeSpent = 5,
                    answeredAt = answeredAt.ToString("O")
                }
            }
        };

        var first = await PostAsUserAsync(userId, "/api/quiz/offline-submit", payload);
        await AssertOkAsync(first);
        var firstJson = await ReadJsonAsync(first);
        Assert.Equal(1, firstJson.GetProperty("importedCount").GetInt32());

        var replay = await PostAsUserAsync(userId, "/api/quiz/offline-submit", payload);
        await AssertOkAsync(replay);
        var replayJson = await ReadJsonAsync(replay);
        Assert.Equal(0, replayJson.GetProperty("importedCount").GetInt32());
        Assert.Equal(firstJson.GetProperty("newXp").GetInt32(), replayJson.GetProperty("newXp").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == userId && x.QuestionId == 1));
        Assert.Equal(2, await db.QuizSessions.CountAsync(x => x.UserId == userId));

        var stat = await db.UserQuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == 1);
        Assert.Equal(1, stat.Attempts);
    }

    private static Dictionary<string, object?> BuildQuizAnswerPayload(
        string? identityField,
        string? identity,
        Guid quizId,
        int questionId,
        string answer)
    {
        var payload = new Dictionary<string, object?>
        {
            ["quizId"] = quizId.ToString(),
            ["questionId"] = questionId,
            ["answer"] = answer,
            ["timeSpentSeconds"] = 5
        };

        if (identityField is not null)
            payload[identityField] = identity;

        return payload;
    }

    private static Dictionary<string, object?> BuildSrsPayload(string? identityField, string? identity)
    {
        var payload = new Dictionary<string, object?>
        {
            ["questionId"] = 1,
            ["isCorrect"] = true,
            ["timeMs"] = 1200
        };

        if (identityField is not null)
            payload[identityField] = identity;

        return payload;
    }

    private async Task<string> GetCorrectAnswerTokenAsync(int questionId = 1)
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

        var response = await PostAsUserAsync(userId, "/api/quiz/start", new
        {
            subtopicId,
            questionCount = 1
        });

        await AssertOkAsync(response);
        var payload = await ReadJsonAsync(response);
        var quizId = Guid.Parse(payload.GetProperty("quizId").GetString()!);
        var questionId = payload.GetProperty("questions").EnumerateArray().Single().GetProperty("id").GetInt32();
        return (quizId, questionId);
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

    private static string NewUserId(string prefix) => $"operation-identity-{prefix}-{Guid.NewGuid():N}";

    private async Task EnsureUserAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (await userManager.FindByIdAsync(userId) is null)
            await userManager.CreateAsync(new IdentityUser { Id = userId, UserName = userId });

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
}

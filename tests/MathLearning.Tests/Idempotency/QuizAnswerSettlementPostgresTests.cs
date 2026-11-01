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

/// <summary>
/// Issue #47: quiz answer settlement against a REAL Postgres schema built only from registered migrations
/// (including 20261101185317_RepairUserAnswerAuditXpSchema). The in-memory suite cannot detect missing
/// physical columns/indexes such as UserAnswerAudits.ClientId or UX_UserAnswerAudits_FirstCorrect_PerQuestion.
/// Gated by POSTGRES_PROVIDER_VALIDATION_REQUIRED=1 and TEST_POSTGRES_MAINTENANCE_CONNECTION_STRING.
/// </summary>
public sealed class QuizAnswerSettlementPostgresTests
{
    private HttpClient _client = null!;
    private PostgresWebApplicationFactory<Program> _factory = null!;

    [Fact]
    [Trait("Category", "PostgresProvider")]
    public async Task QuizAnswerSettlement_Postgres_FirstReplayConflictRollbackAndXpDedup()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("POSTGRES_PROVIDER_VALIDATION_REQUIRED"), "1", StringComparison.Ordinal))
        {
            return;
        }

        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();
        await using var factory = new PostgresWebApplicationFactory<Program>(database);
        _factory = factory;
        _client = factory.CreateClient();

        // 1. First answer: settles once, writes audit row with XP fields.
        var userId = NewUserId("pg-first");
        await EnsureUserAsync(userId);
        var quiz = await StartIssuedQuizAsync(userId);
        var correct = await GetCorrectAnswerTokenAsync(quiz.QuestionId);
        var body = BuildPayload(quiz.QuizId.ToString(), quiz.QuestionId, correct, 5, "pg-op-1", "pg-key-1");
        var first = await PostQuizAnswerAsync(userId, body);
        await AssertOkAsync(first);
        Assert.True((await ReadJsonAsync(first)).GetProperty("isCorrect").GetBoolean());

        // 2. Idempotent replay: same payload -> alreadyProcessed, no double apply.
        var replay = await PostQuizAnswerAsync(userId, body);
        await AssertOkAsync(replay);
        Assert.True((await ReadJsonAsync(replay)).GetProperty("alreadyProcessed").GetBoolean());

        // 3. Conflict: same keys, different answer -> 409 idempotency_conflict.
        var conflict = await PostQuizAnswerAsync(userId,
            BuildPayload(quiz.QuizId.ToString(), quiz.QuestionId, "wrong-answer", 5, "pg-op-1", "pg-key-1"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("idempotency_conflict", (await ReadJsonAsync(conflict)).GetProperty("errorCode").GetString());

        int awardedXpAfterFirst;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var stat = await db.UserQuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == quiz.QuestionId);
            Assert.Equal(1, stat.Attempts);
            Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == userId && x.QuestionId == quiz.QuestionId));
            var audits = await db.UserAnswerAudits.Where(x => x.UserId == userId && x.QuestionId == quiz.QuestionId).ToListAsync();
            Assert.Single(audits);
            Assert.True(audits[0].IsFirstTimeCorrect);
            awardedXpAfterFirst = (await db.UserProfiles.SingleAsync(x => x.UserId == userId)).Xp;
            Assert.True(awardedXpAfterFirst > 0);
        }

        // 4. XP dedup: a new session answering the same question correctly again must not re-award first-time XP
        //    (and must not violate UX_UserAnswerAudits_FirstCorrect_PerQuestion).
        var secondSessionId = await CreateIssuedSessionAsync(userId, new[] { quiz.QuestionId }, DateTime.UtcNow);
        var again = await PostQuizAnswerAsync(userId,
            BuildPayload(secondSessionId.ToString(), quiz.QuestionId, correct, 5, "pg-op-2", "pg-key-2"));
        await AssertOkAsync(again);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            Assert.Equal(1, await db.UserAnswerAudits.CountAsync(
                x => x.UserId == userId && x.QuestionId == quiz.QuestionId && x.IsFirstTimeCorrect));
            Assert.Equal(awardedXpAfterFirst, (await db.UserProfiles.SingleAsync(x => x.UserId == userId)).Xp);
        }

        // 5. Failed transaction: no profile -> 500, no completed ledger row and no answer persisted.
        var rollbackUser = NewUserId("pg-rollback");
        await EnsureUserWithoutProfileAsync(rollbackUser);
        var rq = await StartIssuedQuizAsync(rollbackUser);
        var rCorrect = await GetCorrectAnswerTokenAsync(rq.QuestionId);
        var failed = await PostQuizAnswerAsync(rollbackUser,
            BuildPayload(rq.QuizId.ToString(), rq.QuestionId, rCorrect, 5, "pg-op-rb", "pg-key-rb"));
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            Assert.False(await db.IdempotencyLedgers.AnyAsync(x => x.UserId == rollbackUser &&
                x.OperationType == QuizOperationTypes.QuizAnswer && x.Status == IdempotencyLedgerStatuses.Completed));
            Assert.False(await db.UserAnswers.AnyAsync(x => x.UserId == rollbackUser));
            Assert.False(await db.UserAnswerAudits.AnyAsync(x => x.UserId == rollbackUser));
        }
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
        // Postgres: UserProfiles has an FK to AspNetUsers in the Api database.
        if (!await db.Users.AnyAsync(x => x.Id == userId))
        {
            db.Users.Add(new IdentityUser { Id = userId, UserName = userId, Email = $"{userId}@example.test" });
            await db.SaveChangesAsync();
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
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        if (!await db.Users.AnyAsync(x => x.Id == userId))
        {
            db.Users.Add(new IdentityUser { Id = userId, UserName = userId, Email = $"{userId}@example.test" });
            await db.SaveChangesAsync();
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

    private async Task<int[]> GetQuestionIdsAsync(int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.Questions.OrderBy(x => x.Id).Select(x => x.Id).Take(count).ToArrayAsync();
    }

    private async Task<Guid> CreateIssuedSessionAsync(
        string userId,
        IReadOnlyCollection<int> questionIds,
        DateTime startedAt)
    {
        var session = new QuizSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartedAt = startedAt
        };
        session.SetIssuedQuestionIds(questionIds);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        db.QuizSessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }
}


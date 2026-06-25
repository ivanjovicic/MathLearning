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

public sealed class SrsUpdateIdempotencyTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public SrsUpdateIdempotencyTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FirstSuccess_UpdatesSrsOnce()
    {
        var userId = NewUserId("first-success");
        await EnsureUserAsync(userId);
        const string operationId = "srs-update-first";
        const string idempotencyKey = "srs-update-first-key";

        var response = await PostSrsUpdateAsync(
            userId,
            BuildPayload(
                questionId: 1,
                isCorrect: true,
                timeMs: 1200,
                operationId: operationId,
                idempotencyKey: idempotencyKey));

        await AssertOkAsync(response);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var stat = await db.QuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == 1);
        Assert.Equal(1, stat.SuccessStreak);

        var ledger = await db.IdempotencyLedgers.SingleAsync(
            x => x.UserId == userId && x.OperationType == QuizOperationTypes.SrsUpdate);
        Assert.Equal(IdempotencyLedgerStatuses.Completed, ledger.Status);
    }

    [Fact]
    public async Task DuplicateSamePayload_ReturnsSettledSuccess_WithoutChangingReviewStateAgain()
    {
        var userId = NewUserId("duplicate");
        await EnsureUserAsync(userId);
        const string operationId = "srs-update-dup";
        const string idempotencyKey = "srs-update-dup-key";
        var body = BuildPayload(
            questionId: 1,
            isCorrect: true,
            timeMs: 1200,
            operationId: operationId,
            idempotencyKey: idempotencyKey);

        var first = await PostSrsUpdateAsync(userId, body);
        await AssertOkAsync(first);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var statAfterFirst = await db.QuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == 1);
        var streakAfterFirst = statAfterFirst.SuccessStreak;
        var nextReviewAfterFirst = statAfterFirst.NextReview;

        var replay = await PostSrsUpdateAsync(userId, body);
        await AssertOkAsync(replay);
        var replayPayload = await ReadJsonAsync(replay);
        Assert.True(replayPayload.GetProperty("alreadyProcessed").GetBoolean());

        var statAfterReplay = await db.QuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == 1);
        Assert.Equal(streakAfterFirst, statAfterReplay.SuccessStreak);
        Assert.Equal(nextReviewAfterFirst, statAfterReplay.NextReview);
    }

    [Fact]
    public async Task SameKeysDifferentResult_ReturnsIdempotencyConflict()
    {
        var userId = NewUserId("conflict");
        await EnsureUserAsync(userId);
        const string operationId = "srs-update-conflict";
        const string idempotencyKey = "srs-update-conflict-key";

        var first = await PostSrsUpdateAsync(
            userId,
            BuildPayload(
                questionId: 1,
                isCorrect: true,
                timeMs: 1200,
                operationId: operationId,
                idempotencyKey: idempotencyKey));
        await AssertOkAsync(first);

        var conflict = await PostSrsUpdateAsync(
            userId,
            BuildPayload(
                questionId: 1,
                isCorrect: false,
                timeMs: 1200,
                operationId: operationId,
                idempotencyKey: idempotencyKey));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var conflictPayload = await ReadJsonAsync(conflict);
        Assert.Equal("idempotency_conflict", conflictPayload.GetProperty("errorCode").GetString());
        Assert.True(conflictPayload.GetProperty("conflict").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var stat = await db.QuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == 1);
        Assert.Equal(1, stat.SuccessStreak);
    }

    [Fact]
    public async Task RollbackPath_LeavesNoCompletedLedgerRow()
    {
        var userId = NewUserId("rollback");
        await EnsureUserAsync(userId);
        const string operationId = "srs-update-rollback";
        const string idempotencyKey = "srs-update-rollback-key";

        var response = await PostSrsUpdateAsync(
            userId,
            BuildPayload(
                questionId: 999_999,
                isCorrect: true,
                timeMs: 1200,
                operationId: operationId,
                idempotencyKey: idempotencyKey));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.False(await db.IdempotencyLedgers.AnyAsync(
            x => x.UserId == userId &&
                 x.OperationType == QuizOperationTypes.SrsUpdate &&
                 x.Status == IdempotencyLedgerStatuses.Completed));
        Assert.False(await db.QuestionStats.AnyAsync(x => x.UserId == userId));
    }

    [Fact]
    public async Task DifferentUser_Isolation()
    {
        var userA = NewUserId("user-a");
        var userB = NewUserId("user-b");
        await EnsureUserAsync(userA);
        await EnsureUserAsync(userB);
        const string operationId = "shared-srs-operation";
        const string idempotencyKey = "shared-srs-key";

        var payload = BuildPayload(
            questionId: 1,
            isCorrect: true,
            timeMs: 900,
            operationId: operationId,
            idempotencyKey: idempotencyKey);

        await AssertOkAsync(await PostSrsUpdateAsync(userA, payload));
        await AssertOkAsync(await PostSrsUpdateAsync(userB, payload));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(2, await db.IdempotencyLedgers.CountAsync(
            x => x.OperationId == operationId && x.OperationType == QuizOperationTypes.SrsUpdate));
        Assert.Equal(2, await db.QuestionStats.CountAsync(
            x => (x.UserId == userA || x.UserId == userB) && x.QuestionId == 1 && x.SuccessStreak == 1));
    }

    private static object BuildPayload(
        int questionId,
        bool isCorrect,
        int timeMs,
        string operationId,
        string idempotencyKey)
    {
        return new
        {
            questionId,
            isCorrect,
            timeMs,
            operationId,
            idempotencyKey
        };
    }

    private async Task<HttpResponseMessage> PostSrsUpdateAsync(string userId, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/quiz/srs/update")
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

    private static string NewUserId(string prefix) => $"srs-idem-{prefix}-{Guid.NewGuid():N}";

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
}

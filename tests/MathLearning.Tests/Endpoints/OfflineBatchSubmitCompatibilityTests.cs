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

namespace MathLearning.Tests.Endpoints;

public sealed class OfflineBatchSubmitCompatibilityTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public OfflineBatchSubmitCompatibilityTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OfflineSubmit_ReplayWithMalformedSessionId_DoesNotDoubleAwardXp()
    {
        var userId = NewUserId("offline-submit");
        await EnsureUserAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync();

        var answeredAt = DateTime.UtcNow.AddMinutes(-10);
        var payload = BuildCanonicalPayload(
            sessionId: $"malformed-session-{Guid.NewGuid():N}",
            answers:
            [
                BuildAnswer(1, correctAnswer, timeSpent: 5, answeredAt: answeredAt)
            ]);

        var first = await PostAsUserAsync(userId, "/api/quiz/offline-submit", payload);
        await AssertOkAsync(first);
        var firstJson = await ReadJsonAsync(first);
        Assert.Equal(1, firstJson.GetProperty("importedCount").GetInt32());

        var replay = await PostAsUserAsync(userId, "/api/quiz/offline-submit", payload);
        await AssertOkAsync(replay);
        var replayJson = await ReadJsonAsync(replay);
        Assert.Equal(0, replayJson.GetProperty("importedCount").GetInt32());
        Assert.Equal(firstJson.GetProperty("newXp").GetInt32(), replayJson.GetProperty("newXp").GetInt32());

        await AssertSingleSessionAndAnswerAsync(userId);
    }

    [Theory]
    [InlineData("quizId", "legacy-quiz-1")]
    [InlineData("batchId", "legacy-batch-1")]
    [InlineData("operationId", "legacy-operation-1")]
    public async Task BatchSubmit_LegacyKeys_ReplayWithoutDoubleAwardingXp(string legacyFieldName, string legacyValue)
    {
        var userId = NewUserId($"batch-{legacyFieldName}");
        await EnsureUserAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync();

        var answeredAt = DateTime.UtcNow.AddMinutes(-20);
        var payload = BuildLegacyPayload(
            legacyFieldName,
            legacyValue,
            [
                BuildAnswer(1, correctAnswer, timeSpent: 7, answeredAt: answeredAt)
            ]);

        var first = await PostAsUserAsync(userId, "/api/quiz/batch-submit", payload);
        await AssertOkAsync(first);
        var firstJson = await ReadJsonAsync(first);
        Assert.Equal(1, firstJson.GetProperty("importedCount").GetInt32());

        var replay = await PostAsUserAsync(userId, "/api/quiz/batch-submit", payload);
        await AssertOkAsync(replay);
        var replayJson = await ReadJsonAsync(replay);
        Assert.Equal(0, replayJson.GetProperty("importedCount").GetInt32());
        Assert.Equal(firstJson.GetProperty("newXp").GetInt32(), replayJson.GetProperty("newXp").GetInt32());

        await AssertSingleSessionAndAnswerAsync(userId);
    }

    [Fact]
    public async Task BatchSubmit_DuplicateAndInvalidAnswers_AreCollapsedToOneImport()
    {
        var userId = NewUserId("batch-mixed");
        await EnsureUserAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync();

        var answeredAt = DateTime.UtcNow.AddMinutes(-30);
        var payload = BuildLegacyPayload(
            "batchId",
            $"mixed-batch-{Guid.NewGuid():N}",
            [
                BuildAnswer(1, correctAnswer, timeSpent: 6, answeredAt: answeredAt),
                BuildAnswer(1, correctAnswer, timeSpent: 6, answeredAt: answeredAt),
                BuildAnswer(999_999, "42", timeSpent: 4, answeredAt: answeredAt.AddMinutes(1))
            ]);

        var response = await PostAsUserAsync(userId, "/api/quiz/batch-submit", payload);
        await AssertOkAsync(response);
        var json = await ReadJsonAsync(response);
        Assert.Equal(1, json.GetProperty("importedCount").GetInt32());

        await AssertSingleSessionAndAnswerAsync(userId);
    }

    private static Dictionary<string, object?> BuildCanonicalPayload(string sessionId, IReadOnlyList<object> answers)
        => new()
        {
            ["sessionId"] = sessionId,
            ["answers"] = answers
        };

    private static Dictionary<string, object?> BuildLegacyPayload(
        string fieldName,
        string fieldValue,
        IReadOnlyList<object> answers)
    {
        var payload = new Dictionary<string, object?>
        {
            ["answers"] = answers
        };

        payload[fieldName] = fieldValue;
        return payload;
    }

    private static Dictionary<string, object?> BuildAnswer(
        int questionId,
        string answer,
        int timeSpent,
        DateTime answeredAt)
        => new()
        {
            ["questionId"] = questionId,
            ["answer"] = answer,
            ["timeSpent"] = timeSpent,
            ["isCorrectOffline"] = true,
            ["answeredAt"] = answeredAt.ToString("O")
        };

    private async Task AssertSingleSessionAndAnswerAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        Assert.Equal(1, await db.QuizSessions.CountAsync(x => x.UserId == userId));
        Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == userId));

        var stat = await db.UserQuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == 1);
        Assert.Equal(1, stat.Attempts);
        Assert.Equal(1, stat.CorrectAttempts);
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

    private static string NewUserId(string prefix) => $"offline-batch-{prefix}-{Guid.NewGuid():N}";

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

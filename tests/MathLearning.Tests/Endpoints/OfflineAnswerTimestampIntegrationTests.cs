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

public sealed class OfflineAnswerTimestampIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public OfflineAnswerTimestampIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OfflineSubmit_FutureTimestamp_IsRejectedWithDiagnostic()
    {
        var userId = NewUserId("future");
        await EnsureUserAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync();

        var payload = BuildCanonicalPayload(
            $"future-{Guid.NewGuid():N}",
            [BuildAnswer(1, correctAnswer, 5, DateTime.UtcNow.AddHours(2))]);

        var response = await PostAsUserAsync(userId, "/api/quiz/offline-submit", payload);
        await AssertOkAsync(response);
        var json = await ReadJsonAsync(response);

        Assert.Equal(0, json.GetProperty("importedCount").GetInt32());
        Assert.True(json.TryGetProperty("issues", out var issues));
        Assert.Contains(
            issues.EnumerateArray(),
            x => x.GetProperty("code").GetString() == "timestamp_too_far_in_future");
    }

    [Fact]
    public async Task OfflineSubmit_VeryOldTimestamp_IsRejectedWithDiagnostic()
    {
        var userId = NewUserId("old");
        await EnsureUserAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync();

        var payload = BuildCanonicalPayload(
            $"old-{Guid.NewGuid():N}",
            [BuildAnswer(1, correctAnswer, 5, DateTime.UtcNow.AddDays(-120))]);

        var response = await PostAsUserAsync(userId, "/api/quiz/offline-submit", payload);
        await AssertOkAsync(response);
        var json = await ReadJsonAsync(response);

        Assert.Equal(0, json.GetProperty("importedCount").GetInt32());
        Assert.True(json.TryGetProperty("issues", out var issues));
        Assert.Contains(
            issues.EnumerateArray(),
            x => x.GetProperty("code").GetString() == "timestamp_too_old");
    }

    [Fact]
    public async Task BatchSubmit_MalformedTimestamp_ReturnsDiagnosticWithoutImport()
    {
        var userId = NewUserId("malformed");
        await EnsureUserAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync();

        var payload = new Dictionary<string, object?>
        {
            ["batchId"] = $"malformed-{Guid.NewGuid():N}",
            ["answers"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["questionId"] = 1,
                    ["answer"] = correctAnswer,
                    ["timeSpent"] = 5,
                    ["isCorrectOffline"] = true,
                    ["answeredAt"] = "definitely-not-a-timestamp"
                }
            }
        };

        var response = await PostAsUserAsync(userId, "/api/quiz/batch-submit", payload);
        await AssertOkAsync(response);
        var json = await ReadJsonAsync(response);

        Assert.Equal(0, json.GetProperty("importedCount").GetInt32());
        Assert.Contains(
            json.GetProperty("issues").EnumerateArray(),
            x => x.GetProperty("code").GetString() == "invalid_timestamp");
    }

    [Fact]
    public async Task BatchSubmit_LocalOffsetAndUtcEquivalent_AreTreatedAsSameReplay()
    {
        var userId = NewUserId("offset");
        await EnsureUserAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync();
        var sessionId = $"offset-{Guid.NewGuid():N}";
        var answeredAt = new DateTime(2026, 6, 24, 10, 0, 0, 123, DateTimeKind.Utc);

        var firstPayload = new Dictionary<string, object?>
        {
            ["sessionId"] = sessionId,
            ["answers"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["questionId"] = 1,
                    ["answer"] = correctAnswer,
                    ["timeSpent"] = 5,
                    ["isCorrectOffline"] = true,
                    ["answeredAt"] = new DateTimeOffset(answeredAt).ToOffset(TimeSpan.FromHours(2)).ToString("O")
                }
            }
        };

        var first = await PostAsUserAsync(userId, "/api/quiz/offline-submit", firstPayload);
        await AssertOkAsync(first);
        Assert.Equal(1, (await ReadJsonAsync(first)).GetProperty("importedCount").GetInt32());

        var replayPayload = new Dictionary<string, object?>
        {
            ["sessionId"] = sessionId,
            ["answers"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["questionId"] = 1,
                    ["answer"] = correctAnswer,
                    ["timeSpent"] = 5,
                    ["isCorrectOffline"] = true,
                    ["answeredAt"] = answeredAt.ToString("O")
                }
            }
        };

        var replay = await PostAsUserAsync(userId, "/api/quiz/offline-submit", replayPayload);
        await AssertOkAsync(replay);
        Assert.Equal(0, (await ReadJsonAsync(replay)).GetProperty("importedCount").GetInt32());
        await AssertSingleAnswerAsync(userId);
    }

    [Fact]
    public async Task OfflineSubmit_PrecisionVariants_CollapseToSingleImport()
    {
        var userId = NewUserId("precision");
        await EnsureUserAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync();
        var sessionId = $"precision-{Guid.NewGuid():N}";
        var baseTime = new DateTime(2026, 6, 20, 8, 30, 15, 120, DateTimeKind.Utc);

        var firstPayload = BuildCanonicalPayload(
            sessionId,
            [BuildAnswer(1, correctAnswer, 5, baseTime.AddTicks(4567))]);

        var first = await PostAsUserAsync(userId, "/api/quiz/offline-submit", firstPayload);
        await AssertOkAsync(first);
        Assert.Equal(1, (await ReadJsonAsync(first)).GetProperty("importedCount").GetInt32());

        var replayPayload = BuildCanonicalPayload(
            sessionId,
            [BuildAnswer(1, correctAnswer, 5, baseTime.AddTicks(4999))]);

        var replay = await PostAsUserAsync(userId, "/api/quiz/offline-submit", replayPayload);
        await AssertOkAsync(replay);
        Assert.Equal(0, (await ReadJsonAsync(replay)).GetProperty("importedCount").GetInt32());
        await AssertSingleAnswerAsync(userId);
    }

    private static string NewUserId(string prefix) => $"offline-ts-{prefix}-{Guid.NewGuid():N}";

    private static Dictionary<string, object?> BuildCanonicalPayload(string sessionId, IReadOnlyList<object> answers)
        => new()
        {
            ["sessionId"] = sessionId,
            ["answers"] = answers
        };

    private static Dictionary<string, object?> BuildAnswer(int questionId, string answer, int timeSpent, DateTime answeredAt)
        => new()
        {
            ["questionId"] = questionId,
            ["answer"] = answer,
            ["timeSpent"] = timeSpent,
            ["isCorrectOffline"] = true,
            ["answeredAt"] = answeredAt.ToString("O")
        };

    private async Task EnsureUserAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
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

        if (!await db.UserProfiles.AnyAsync(p => p.UserId == userId))
        {
            var now = DateTime.UtcNow;
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Username = userId,
                DisplayName = userId,
                Coins = 0,
                Level = 1,
                Xp = 0,
                Streak = 0,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }
    }

    private async Task<string> GetCorrectAnswerTokenAsync(int questionId = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var question = await db.Questions.Include(q => q.Options).SingleAsync(q => q.Id == questionId);
        var correctOption = question.Options.First(o => o.IsCorrect);
        return correctOption.Id > 0
            ? correctOption.Id.ToString()
            : question.CorrectAnswer ?? throw new InvalidOperationException($"Question {questionId} has no correct answer");
    }

    private async Task AssertSingleAnswerAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == userId && x.QuestionId == 1));
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
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement;
    }
}

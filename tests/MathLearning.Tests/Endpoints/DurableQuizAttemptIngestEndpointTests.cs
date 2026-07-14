using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class DurableQuizAttemptIngestEndpointTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient client;
    private readonly CustomWebApplicationFactory<Program> factory;

    public DurableQuizAttemptIngestEndpointTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task QuizAnswer_SettlesImmediately_WhileOutboxCommandRemainsRecoverable()
    {
        var userId = NewUserId("quiz-outbox");
        await EnsureUserAsync(userId);
        var outboxBefore = await CountPendingIngestOutboxAsync();
        var issuedQuiz = await StartIssuedQuizAsync(userId);
        var correctAnswer = await GetCorrectAnswerTokenAsync(issuedQuiz.QuestionId);
        var payload = new
        {
            quizId = issuedQuiz.QuizId.ToString(),
            questionId = issuedQuiz.QuestionId,
            answer = correctAnswer,
            timeSpentSeconds = 5,
            operationId = $"quiz-outbox-{Guid.NewGuid():N}",
            idempotencyKey = $"quiz-outbox-key-{Guid.NewGuid():N}"
        };

        var response = await PostAsUserAsync(userId, "/api/quiz/answer", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == userId && x.QuestionId == issuedQuiz.QuestionId));
            Assert.Equal(0, await db.QuizAttempts.CountAsync());
            Assert.Equal(outboxBefore + 1, await CountPendingIngestOutboxAsync());
        }

        var replay = await PostAsUserAsync(userId, "/api/quiz/answer", payload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);

        using var replayJson = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.True(replayJson.RootElement.GetProperty("alreadyProcessed").GetBoolean());

        using (var scope = factory.Services.CreateScope())
        {
            Assert.Equal(outboxBefore + 1, await CountPendingIngestOutboxAsync());
        }
    }

    [Fact]
    public async Task OfflineSubmit_ReturnsSuccess_AndLeavesDurablePendingIngestCommands()
    {
        var userId = NewUserId("offline-outbox");
        await EnsureUserAsync(userId);
        var outboxBefore = await CountPendingIngestOutboxAsync();
        var correctAnswer = await GetCorrectAnswerTokenAsync(1);
        var answeredAt = DateTime.UtcNow.AddMinutes(-5);

        var response = await PostAsUserAsync(
            userId,
            "/api/quiz/offline-submit",
            new
            {
                sessionId = Guid.NewGuid().ToString(),
                answers = new[]
                {
                    new
                    {
                        questionId = 1,
                        answer = correctAnswer,
                        timeSpent = 7,
                        isCorrectOffline = false,
                        answeredAt = answeredAt.ToString("O")
                    }
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, responseJson.RootElement.GetProperty("importedCount").GetInt32());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == userId));
        Assert.Equal(0, await db.QuizAttempts.CountAsync());
        Assert.Equal(outboxBefore + 1, await CountPendingIngestOutboxAsync());
    }

    private static string NewUserId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private async Task EnsureUserAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        if (await userManager.FindByIdAsync(userId) is null)
        {
            var create = await userManager.CreateAsync(new IdentityUser { Id = userId, UserName = userId });
            Assert.True(create.Succeeded, string.Join(", ", create.Errors.Select(x => x.Description)));
        }

        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
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

    private async Task<(Guid QuizId, int QuestionId)> StartIssuedQuizAsync(string userId)
    {
        var response = await PostAsUserAsync(userId, "/api/quiz/start", new
        {
            subtopicId = 1,
            questionCount = 1
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var quizId = json.RootElement.GetProperty("quizId").GetGuid();
        var questionId = json.RootElement.GetProperty("questions")[0].GetProperty("id").GetInt32();
        return (quizId, questionId);
    }

    private async Task<string> GetCorrectAnswerTokenAsync(int questionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var question = await db.Questions.Include(x => x.Options).SingleAsync(x => x.Id == questionId);
        return question.Options.Single(x => x.IsCorrect).Id.ToString();
    }

    private Task<HttpResponseMessage> PostAsUserAsync(string userId, string url, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Test-UserId", userId);
        return client.SendAsync(request);
    }

    private async Task<int> CountPendingIngestOutboxAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        return await db.Outbox.CountAsync(x =>
            x.ProcessedUtc == null &&
            x.Type.Contains("QuizAttemptIngestRequested", StringComparison.Ordinal));
    }
}

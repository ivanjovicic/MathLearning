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
/// B0.3: issued V1 must keep grading, explanation, XP and idempotent replay after V2 publish.
/// </summary>
public sealed class QuizIssuedVersionSettlementIntegrationTests
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string V1Explanation = "B0.3-V1-explanation-seven-rows-of-eight";
    private const string V2Explanation = "B0.3-V2-explanation-should-never-surface";
    private const string V1StepText = "B0.3-V1-step-structure";
    private const string V2StepText = "B0.3-V2-step-should-never-surface";

    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public QuizIssuedVersionSettlementIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IssuedV1_PublishedV2_SubmitV1_PreservesGradingExplanationXpAndReplay()
    {
        var userId = $"quiz-issued-v1-{Guid.NewGuid():N}";
        await EnsureUserAsync(userId);

        var prepared = await PrepareIssuedV1QuestionAsync();
        // Freeze V1 on two sessions before publish: one-question sessions complete after any answer.
        var explanationSession = await StartIssuedQuizAsync(userId, prepared.SubtopicId);
        var gradingSession = await StartIssuedQuizAsync(userId, prepared.SubtopicId);
        Assert.Equal(prepared.QuestionId, explanationSession.QuestionId);
        Assert.Equal(prepared.QuestionId, gradingSession.QuestionId);

        await PublishV2MutatingLiveQuestionAsync(
            prepared.QuestionId,
            prepared.V1CorrectOptionId,
            prepared.V2CorrectOptionId);

        var wrongPayload = BuildPayload(
            quizId: explanationSession.QuizId.ToString(),
            questionId: explanationSession.QuestionId,
            answer: prepared.V2CorrectOptionId.ToString(),
            timeSpentSeconds: 4,
            operationId: "b03-wrong-v1",
            idempotencyKey: "b03-wrong-v1-key");

        var wrongResponse = await PostQuizAnswerAsync(userId, wrongPayload);
        await AssertOkAsync(wrongResponse);
        var wrongBody = await ReadJsonAsync(wrongResponse);

        Assert.False(wrongBody.GetProperty("isCorrect").GetBoolean());
        Assert.Equal(0, wrongBody.GetProperty("awardedXp").GetInt32());
        Assert.Contains(V1Explanation, wrongBody.GetProperty("explanation").GetString());
        Assert.DoesNotContain(V2Explanation, wrongBody.GetProperty("explanation").GetString());
        var wrongSteps = wrongBody.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("text").GetString()).ToList();
        Assert.Contains(wrongSteps, text => text != null && text.Contains(V1StepText, StringComparison.Ordinal));
        Assert.DoesNotContain(wrongSteps, text => text != null && text.Contains(V2StepText, StringComparison.Ordinal));

        var correctPayload = BuildPayload(
            quizId: gradingSession.QuizId.ToString(),
            questionId: gradingSession.QuestionId,
            answer: prepared.V1CorrectOptionId.ToString(),
            timeSpentSeconds: 6,
            operationId: "b03-correct-v1",
            idempotencyKey: "b03-correct-v1-key");

        var correctResponse = await PostQuizAnswerAsync(userId, correctPayload);
        await AssertOkAsync(correctResponse);
        var correctBody = await ReadJsonAsync(correctResponse);

        Assert.True(correctBody.GetProperty("isCorrect").GetBoolean());
        Assert.True(correctBody.GetProperty("isFirstTimeCorrect").GetBoolean());
        Assert.Equal(10, correctBody.GetProperty("awardedXp").GetInt32());
        var totalXpAfterFirstCorrect = correctBody.GetProperty("totalXp").GetInt32();
        Assert.True(totalXpAfterFirstCorrect >= 10);

        var replayResponse = await PostQuizAnswerAsync(userId, correctPayload);
        await AssertOkAsync(replayResponse);
        var replayBody = await ReadJsonAsync(replayResponse);

        Assert.True(replayBody.GetProperty("alreadyProcessed").GetBoolean());
        Assert.True(replayBody.GetProperty("isCorrect").GetBoolean());
        Assert.Equal(10, replayBody.GetProperty("awardedXp").GetInt32());
        Assert.Equal(totalXpAfterFirstCorrect, replayBody.GetProperty("totalXp").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var profile = await db.UserProfiles.SingleAsync(x => x.UserId == userId);
        Assert.Equal(totalXpAfterFirstCorrect, profile.Xp);

        var answers = await db.UserAnswers
            .Where(x => x.UserId == userId && x.QuestionId == prepared.QuestionId)
            .ToListAsync();
        Assert.Equal(2, answers.Count);
        Assert.Contains(answers, a => !a.IsCorrect);
        Assert.Contains(answers, a => a.IsCorrect);

        var audits = await db.UserAnswerAudits
            .Where(x => x.UserId == userId && x.QuestionId == prepared.QuestionId)
            .ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Equal(10, audits.Sum(a => a.AwardedXp));
    }

    private async Task<(int QuestionId, int SubtopicId, int V1CorrectOptionId, int V2CorrectOptionId)> PrepareIssuedV1QuestionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var category = await db.Categories.OrderBy(x => x.Id).FirstAsync();
        var topic = await db.Topics.OrderBy(x => x.Id).FirstAsync();
        var subtopic = new Subtopic($"B0.3 issued-{Guid.NewGuid():N}", topic.Id);
        db.Subtopics.Add(subtopic);
        await db.SaveChangesAsync();

        var question = new Question("Koliko je 7 × 8?", 1, category.Id, V1Explanation);
        question.SetSubtopic(subtopic.Id);
        question.SetType("multiple_choice");
        question.ReplaceOptions(
        [
            new QuestionOption("56", true, order: 0),
            new QuestionOption("54", false, order: 1),
            new QuestionOption("64", false, order: 2),
            new QuestionOption("48", false, order: 3)
        ]);
        question.ReplaceSteps(
        [
            new QuestionStep(0, 1, V1StepText, hint: null, highlight: true)
        ]);
        question.SetCurrentVersionNumber(1);
        question.SetPublishState(QuestionPublishStates.Published, "b0.3-test", DateTime.UtcNow);
        db.Questions.Add(question);
        await db.SaveChangesAsync();

        // QuestionStep requires QuestionId; rewrite with persisted id.
        question.ReplaceSteps(
        [
            new QuestionStep(question.Id, 1, V1StepText, hint: null, highlight: true)
        ]);
        await db.SaveChangesAsync();

        var options = await db.Questions
            .Include(q => q.Options)
            .Where(q => q.Id == question.Id)
            .SelectMany(q => q.Options)
            .OrderBy(o => o.Order)
            .ToListAsync();

        var v1Correct = options.Single(o => o.IsCorrect).Id;
        var v2Correct = options.First(o => !o.IsCorrect).Id;
        return (question.Id, subtopic.Id, v1Correct, v2Correct);
    }

    private async Task PublishV2MutatingLiveQuestionAsync(
        int questionId,
        int v1CorrectOptionId,
        int v2CorrectOptionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var question = await db.Questions
            .Include(q => q.Options)
            .Include(q => q.Steps)
            .SingleAsync(q => q.Id == questionId);

        foreach (var option in question.Options.OrderBy(o => o.Order))
        {
            var isCorrect = option.Id == v2CorrectOptionId;
            option.Update(option.Text, isCorrect, option.Order);
        }

        question.SetCorrectOptionId(v2CorrectOptionId);
        question.SetExplanation(V2Explanation);
        question.ReplaceSteps(
        [
            new QuestionStep(question.Id, 1, V2StepText, hint: null, highlight: true)
        ]);
        question.SetCurrentVersionNumber(2);
        question.SetPublishState(QuestionPublishStates.Published, "b0.3-v2", DateTime.UtcNow);
        await db.SaveChangesAsync();

        // Sanity: live row now grades V2-correct as correct.
        var live = await db.Questions.Include(q => q.Options).SingleAsync(q => q.Id == questionId);
        Assert.Contains(live.Options, o => o.Id == v2CorrectOptionId && o.IsCorrect);
        Assert.Contains(live.Options, o => o.Id == v1CorrectOptionId && !o.IsCorrect);
        Assert.Equal(V2Explanation, live.Explanation);
        Assert.Equal(2, live.CurrentVersionNumber);
    }

    private async Task<(Guid QuizId, int QuestionId)> StartIssuedQuizAsync(string userId, int subtopicId)
    {
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

    private async Task<HttpResponseMessage> PostQuizAnswerAsync(string userId, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/quiz/answer")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostAsUserAsync(string userId, string url, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await _client.SendAsync(request);
    }

    private static string BuildPayload(
        string quizId,
        int questionId,
        string answer,
        int timeSpentSeconds,
        string operationId,
        string idempotencyKey)
    {
        return JsonSerializer.Serialize(new
        {
            quizId,
            questionId,
            answer,
            timeSpentSeconds,
            operationId,
            idempotencyKey
        });
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
}

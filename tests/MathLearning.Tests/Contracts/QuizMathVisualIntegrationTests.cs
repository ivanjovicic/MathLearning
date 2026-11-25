using System.Net;
using System.Text;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.QuestionAuthoring;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MathLearning.Tests.Contracts;

public class QuizMathVisualIntegrationTests
{
    [Fact]
    public async Task ApprovedV1_IsPostOnly_VersionBound_UserScoped_AndStableOnReplay()
    {
        var authored = CreateAuthoredQuestion();
        var hash = MathVisualContentPolicy.ComputeContentHash(authored);
        var config = new Dictionary<string, string?>
        {
            ["MathVisual:Enabled"] = "true",
            ["MathVisual:ManifestSigningKey"] = Convert.ToBase64String(Enumerable.Range(0, 32).Select(x => (byte)x).ToArray()),
            ["MathVisual:PackSubtopicIds:0"] = "1",
            ["MathVisual:ApprovedContentHashes:0"] = hash
        };
        using var factory = new CustomWebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<MathVisualContentPolicy>();
                services.AddSingleton(new MathVisualContentPolicy([hash], [1]));
            });
        });
        using var client = factory.CreateClient();
        const string userId = "mathvisual-test-user";
        int questionId;
        int correctOptionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            await userManager.CreateAsync(new IdentityUser { Id = userId, UserName = userId });
            db.UserProfiles.Add(new UserProfile { UserId = userId, Xp = 0, Level = 1, Coins = 0, UpdatedAt = DateTime.UtcNow });
            Assert.Equal(1, db.Categories.First().Id);
            Assert.Equal(1, db.Subtopics.First().Id);
            var question = new Question(authored.Text, 2, 1, authored.Explanation);
            question.SetSubtopic(1);
            question.SetType("multiple_choice");
            question.SetCorrectAnswer("56");
            question.ReplaceOptions([new QuestionOption("56", true, 0), new QuestionOption("54", false, 1)]);
            question.ReplaceSteps(authored.Steps.Select(x => new QuestionStep(0, x.Order, x.Text, null, x.Highlight)).ToList());
            question.SetCurrentVersionNumber(1);
            question.SetPublishState(QuestionPublishStates.Published, "fixture", DateTime.UtcNow);
            db.Questions.Add(question);
            await db.SaveChangesAsync();
            questionId = question.Id;
            question.ReplaceSteps(authored.Steps.Select(x => new QuestionStep(questionId, x.Order, x.Text, null, x.Highlight)).ToList());
            db.QuestionVersions.Add(new QuestionVersion
            {
                QuestionId = questionId,
                SourceDraftId = Guid.NewGuid(),
                VersionNumber = 1,
                SnapshotJson = JsonSerializer.Serialize(authored, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                PublishState = QuestionPublishStates.Published
            });
            await db.SaveChangesAsync();
            correctOptionId = question.Options.Single(x => x.IsCorrect).Id;
            var runtimeConfig = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            Assert.True(runtimeConfig.GetValue<bool>("MathVisual:Enabled"));
            Assert.Empty(scope.ServiceProvider.GetRequiredService<MathVisualContentPolicy>().Validate(authored, hash));
            var loaded = await db.Questions.AsNoTracking()
                .Include(x => x.Options).Include(x => x.Steps).SingleAsync(x => x.Id == questionId);
            var previewAids = await scope.ServiceProvider.GetRequiredService<MathLearning.Api.Services.IssuedMathVisualService>()
                .BuildAsync([loaded], "sr", Guid.NewGuid(), userId, CancellationToken.None);
            Assert.Single(previewAids);
        }

        using var start = Request(HttpMethod.Post, "/api/quiz/start", userId,
            new { subtopicId = 1, questionCount = 25 });
        using var startResponse = await client.SendAsync(start);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var startJson = await startResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("mathVisual", startJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reviewedContentHash", startJson, StringComparison.OrdinalIgnoreCase);
        using var startDocument = JsonDocument.Parse(startJson);
        var quizId = startDocument.RootElement.GetProperty("quizId").GetGuid();
        Assert.Contains(startDocument.RootElement.GetProperty("questions").EnumerateArray(),
            x => x.GetProperty("id").GetInt32() == questionId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var session = await db.QuizSessions.SingleAsync(x => x.Id == quizId);
            Assert.NotNull(session.GetIssuedQuestionSnapshot(questionId)?.MathVisual);
            var question = await db.Questions.Include(x => x.Options).Include(x => x.Steps).SingleAsync(x => x.Id == questionId);
            question.SetText("Koliko je 7 × 9?");
            question.SetExplanation("V2 explanation");
            question.ReplaceOptions([new QuestionOption("63", true, 0), new QuestionOption("62", false, 1)]);
            question.SetCorrectAnswer("63");
            question.SetCurrentVersionNumber(2);
            question.ReplaceSteps([new QuestionStep(questionId, 1, "7 × 9 = 63", null, true)]);
            db.QuestionVersions.Add(new QuestionVersion
            {
                QuestionId = questionId,
                SourceDraftId = Guid.NewGuid(),
                VersionNumber = 2,
                SnapshotJson = JsonSerializer.Serialize(authored with
                {
                    Text = "Koliko je 7 × 9?",
                    CorrectAnswer = "63",
                    Explanation = "V2 explanation",
                    Options = [new(null, "63", true), new(null, "62", false)],
                    Steps = [new(1, "7 × 9 = 63", null, true)],
                    MathVisualPlan = new(1, "directArray", 1, "sr", "multiply", "product", 7, 9, [7])
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                PublishState = QuestionPublishStates.Published
            });
            await db.SaveChangesAsync();
        }

        var answerBody = new
        {
            quizId = quizId.ToString(), questionId, answer = correctOptionId.ToString(),
            timeSpentSeconds = 4, operationId = "visual-v1-op", idempotencyKey = "visual-v1-key"
        };
        using var foreign = Request(HttpMethod.Post, "/api/quiz/answer", "other-user", answerBody);
        using var foreignResponse = await client.SendAsync(foreign);
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);

        using var answer = Request(HttpMethod.Post, "/api/quiz/answer", userId, answerBody);
        using var answerResponse = await client.SendAsync(answer);
        Assert.Equal(HttpStatusCode.OK, answerResponse.StatusCode);
        var answerJson = await answerResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("integrityTag", answerJson, StringComparison.OrdinalIgnoreCase);
        using var answerDocument = JsonDocument.Parse(answerJson);
        var visual = answerDocument.RootElement.GetProperty("mathVisual");
        Assert.Equal(1, visual.GetProperty("issuedQuestionVersion").GetInt32());
        Assert.Equal(hash, visual.GetProperty("reviewedContentHash").GetString());
        Assert.Equal(7, visual.GetProperty("rows").GetInt32());
        Assert.Equal(8, visual.GetProperty("columns").GetInt32());
        Assert.DoesNotContain("V2 explanation", answerJson);

        using var replayA = Request(HttpMethod.Post, "/api/quiz/answer", userId, answerBody);
        using var replayB = Request(HttpMethod.Post, "/api/quiz/answer", userId, answerBody);
        var replayResponses = await Task.WhenAll(client.SendAsync(replayA), client.SendAsync(replayB));
        using var replayResponseA = replayResponses[0];
        using var replayResponseB = replayResponses[1];
        using var replayDocumentA = JsonDocument.Parse(await replayResponseA.Content.ReadAsStringAsync());
        using var replayDocumentB = JsonDocument.Parse(await replayResponseB.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, replayResponseA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponseB.StatusCode);
        Assert.True(replayDocumentA.RootElement.GetProperty("alreadyProcessed").GetBoolean());
        Assert.True(replayDocumentB.RootElement.GetProperty("alreadyProcessed").GetBoolean());
        Assert.Equal(hash, replayDocumentA.RootElement.GetProperty("mathVisual").GetProperty("reviewedContentHash").GetString());
        Assert.Equal(hash, replayDocumentB.RootElement.GetProperty("mathVisual").GetProperty("reviewedContentHash").GetString());
        var firstAward = answerDocument.RootElement.GetProperty("awardedXp").GetInt32();
        Assert.True(answerDocument.RootElement.GetProperty("isCorrect").GetBoolean());
        Assert.True(firstAward > 0);
        Assert.Equal(firstAward, replayDocumentA.RootElement.GetProperty("awardedXp").GetInt32());
        Assert.Equal(firstAward, replayDocumentB.RootElement.GetProperty("awardedXp").GetInt32());
        Assert.Equal(answerDocument.RootElement.GetProperty("totalXp").GetInt32(),
            replayDocumentA.RootElement.GetProperty("totalXp").GetInt32());
        Assert.Equal(answerDocument.RootElement.GetProperty("totalXp").GetInt32(),
            replayDocumentB.RootElement.GetProperty("totalXp").GetInt32());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var profile = await db.UserProfiles.SingleAsync(x => x.UserId == userId);
            Assert.Equal(firstAward, profile.Xp);
        }
    }

    private static QuestionAuthoringRequest CreateAuthoredQuestion() => new(
        null, "Koliko je 7 × 8?", "multiple_choice", "56", "Sedam redova po osam.",
        2, 1, 1, [new(null, "56", true), new(null, "54", false)], [],
        [new(1, "5 × 8 = 40", null, false), new(2, "2 × 8 = 16", null, false), new(3, "40 + 16 = 56", null, true)],
        ContentPackId: MathVisualContentPolicy.PackId,
        MathVisualPlan: new(1, "splitFive", 1, "sr", "multiply", "product", 7, 8, [5, 2]),
        ContentLocale: "sr");

    private static HttpRequestMessage Request(HttpMethod method, string path, string userId, object body)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);
        return request;
    }
}

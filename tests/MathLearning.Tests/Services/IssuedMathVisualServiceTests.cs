using System.Text.Json;
using MathLearning.Api.Services;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.DTOs.Quiz;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Services.QuestionAuthoring;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MathLearning.Tests.Services;

public class IssuedMathVisualServiceTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task IssuedV1Snapshot_LiveV2Mutation_StillReturnsSignedV1PostAid_AndRejectsTampering()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var (question, authored) = await CreateReviewedQuestionAsync(db);
        var hash = MathVisualContentPolicy.ComputeContentHash(authored);
        var policy = new MathVisualContentPolicy([hash], [question.SubtopicId]);
        var service = CreateService(db, policy, enabled: true);
        var session = new QuizSession { Id = Guid.NewGuid(), UserId = "visual-owner", StartedAt = DateTime.UtcNow };
        Assert.Equal("sr", service.ResolveLocale(question, "en"));
        Assert.True(service.IsDesignatedPackSubtopic(question));

        var aids = await service.BuildAsync([question], "sr", session.Id, session.UserId, CancellationToken.None);
        Assert.Single(aids);
        session.SetIssuedQuestions([question], aids);
        var issued = session.GetIssuedQuestionSnapshot(question.Id)!;
        var v1 = service.ValidateForResponse(issued, "sr", session.Id, session.UserId);
        Assert.NotNull(v1);
        Assert.Equal(7, v1.Rows);
        Assert.Equal(8, v1.Columns);
        Assert.Equal([5, 2], v1.Groups);
        Assert.Equal(1, v1.IssuedQuestionVersion);
        Assert.Equal(hash, v1.ReviewedContentHash);

        question.SetText("Koliko je 7 × 9?");
        question.SetCorrectAnswer("63");
        question.SetCurrentVersionNumber(2);
        question.ReplaceSteps([new QuestionStep(question.Id, 1, "7 × 9 = 63", null, false)]);
        Assert.Equal(7, service.ValidateForResponse(issued, "sr", session.Id, session.UserId)!.Rows);

        Assert.Null(service.ValidateForResponse(issued, "en", session.Id, session.UserId));
        Assert.Null(service.ValidateForResponse(
            issued with { MathVisual = v1 with { Locale = "en" } }, "en", session.Id, session.UserId));
        Assert.Null(service.ValidateForResponse(issued, "sr", session.Id, "other-user"));
        Assert.Null(service.ValidateForResponse(issued with { VersionNumber = 0 }, "sr", session.Id, session.UserId));
        Assert.Null(service.ValidateForResponse(issued with { MathVisual = v1 with { Rows = 8, Columns = 7 } }, "sr", session.Id, session.UserId));
        Assert.Null(service.ValidateForResponse(issued with { MathVisual = v1 with { SchemaVersion = 99 } }, "sr", session.Id, session.UserId));
        Assert.Null(service.ValidateForResponse(issued with { Options = [] }, "sr", session.Id, session.UserId));
        Assert.Null(service.ValidateForResponse(issued with { Steps = [] }, "sr", session.Id, session.UserId));
        Assert.Null(service.ValidateForResponse(issued with { Explanation = "V2 explanation" }, "sr", session.Id, session.UserId));
    }

    [Fact]
    public async Task BuildAsync_RejectsLiveOptionsThatDifferFromThePublishedSnapshot()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var (question, authored) = await CreateReviewedQuestionAsync(db);
        var policy = new MathVisualContentPolicy(
            [MathVisualContentPolicy.ComputeContentHash(authored)], [question.SubtopicId]);
        var service = CreateService(db, policy, enabled: true);
        question.ReplaceOptions([new QuestionOption("56", true, 0), new QuestionOption("53", false, 1)]);

        var aids = await service.BuildAsync([question], "sr", Guid.NewGuid(), "visual-owner", CancellationToken.None);

        Assert.Empty(aids);
    }

    [Fact]
    public async Task DefaultOff_AndLegacyIdOnly_DoNotCreateAid_OrLeakPreAnswerJson()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var (question, authored) = await CreateReviewedQuestionAsync(db);
        var policy = new MathVisualContentPolicy(
            [MathVisualContentPolicy.ComputeContentHash(authored)], [question.SubtopicId]);
        var off = CreateService(db, policy, enabled: false);
        var session = new QuizSession { Id = Guid.NewGuid(), UserId = "visual-owner", StartedAt = DateTime.UtcNow };
        Assert.Empty(await off.BuildAsync([question], "sr", session.Id, session.UserId, CancellationToken.None));

        session.SetIssuedQuestionIds([question.Id]);
        Assert.Null(session.GetIssuedQuestionSnapshot(question.Id));
        Assert.Null(off.ValidateForResponse(session.GetIssuedQuestionSnapshot(question.Id), "sr", session.Id, session.UserId));

        var questionDto = new QuizQuestionDto(question.Id, question.Type, question.Text, [], question.Difficulty, null, null);
        var preJson = JsonSerializer.Serialize(questionDto, WebJson);
        Assert.DoesNotContain("mathVisual", preJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reviewedContentHash", preJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PostContract_IsOptionalAndOldClientCanIgnoreIt()
    {
        var aid = new IssuedMathVisualDto(1, 9, 1, new string('A', 64), "splitFive", 1,
            "sr", "multiply", "product", 7, 8, "multiplication_array", [5, 2],
            [new(1, "5 × 8 = 40"), new(2, "2 × 8 = 16"), new(3, "40 + 16 = 56")],
            "total", "server_confirmed");
        var post = JsonSerializer.Serialize(new SubmitAnswerResponse(false, "Objašnjenje", MathVisual: aid), WebJson);
        Assert.Contains("\"mathVisual\"", post);
        Assert.Contains("\"issuedQuestionVersion\":1", post);
        Assert.Contains("\"rows\":7", post);
        Assert.Contains("\"columns\":8", post);
        Assert.Contains("\"answerSettlement\":\"server_confirmed\"", post);
        Assert.DoesNotContain("integrityTag", post, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(JsonSerializer.Deserialize<OldSubmitAnswerResponse>(post, WebJson));

        var oldPost = JsonSerializer.Serialize(new SubmitAnswerResponse(false, "Objašnjenje"), WebJson);
        Assert.DoesNotContain("mathVisual", oldPost, StringComparison.OrdinalIgnoreCase);
    }

    private static IssuedMathVisualService CreateService(
        MathLearning.Infrastructure.Persistance.ApiDbContext db,
        MathVisualContentPolicy policy,
        bool enabled)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MathVisual:Enabled"] = enabled.ToString(),
            ["MathVisual:ManifestSigningKey"] = Convert.ToBase64String(Enumerable.Range(0, 32).Select(x => (byte)x).ToArray())
        }).Build();
        return new IssuedMathVisualService(db, policy, config);
    }

    private static async Task<(Question Question, QuestionAuthoringRequest Authored)> CreateReviewedQuestionAsync(
        MathLearning.Infrastructure.Persistance.ApiDbContext db)
    {
        var categoryId = db.Categories.First().Id;
        var subtopicId = db.Subtopics.First().Id;
        var text = "Koliko je 7 × 8?";
        var steps = new StepExplanationAuthoringDto[]
        {
            new(1, "5 × 8 = 40", null, false),
            new(2, "2 × 8 = 16", null, false),
            new(3, "40 + 16 = 56", null, true)
        };
        var authored = new QuestionAuthoringRequest(null, text, "multiple_choice", "56", "Sedam redova po osam.",
            2, categoryId, subtopicId,
            [new(null, "56", true), new(null, "54", false)], [], steps,
            ContentPackId: MathVisualContentPolicy.PackId,
            MathVisualPlan: new(1, "splitFive", 1, "sr", "multiply", "product", 7, 8, [5, 2]),
            ContentLocale: "sr");
        var question = new Question(text, 2, categoryId, authored.Explanation);
        question.SetSubtopic(subtopicId);
        question.SetType("multiple_choice");
        question.SetCorrectAnswer("56");
        question.ReplaceOptions([new QuestionOption("56", true, 0), new QuestionOption("54", false, 1)]);
        question.ReplaceSteps(steps.Select(x => new QuestionStep(0, x.Order, x.Text, null, x.Highlight)).ToList());
        question.SetCurrentVersionNumber(1);
        question.SetPublishState(QuestionPublishStates.Published, "fixture", DateTime.UtcNow);
        db.Questions.Add(question);
        await db.SaveChangesAsync();
        question.ReplaceSteps(steps.Select(x => new QuestionStep(question.Id, x.Order, x.Text, null, x.Highlight)).ToList());
        db.QuestionVersions.Add(new QuestionVersion
        {
            QuestionId = question.Id,
            SourceDraftId = Guid.NewGuid(),
            VersionNumber = 1,
            SnapshotJson = JsonSerializer.Serialize(authored, WebJson),
            PublishState = QuestionPublishStates.Published
        });
        await db.SaveChangesAsync();
        return (question, authored);
    }

    private sealed record OldSubmitAnswerResponse(bool IsCorrect, string? Explanation, List<StepExplanationDto>? Steps);
}

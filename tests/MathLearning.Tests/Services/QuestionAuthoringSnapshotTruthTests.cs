using System.Text.Json;
using MathLearning.Application.Content;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Validators;
using MathLearning.Domain.Entities;
using MathLearning.Domain.Enums;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Performance;
using MathLearning.Infrastructure.Services.QuestionAuthoring;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class QuestionAuthoringSnapshotTruthTests
{
    [Fact]
    public async Task CreateQuestionSnapshot_RoundTripsFullAuthoredState()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var shared = CreateSharedAuthoringService();

        var create = await shared.CreateQuestionAsync(
            db,
            new QuestionAuthoringRequest(
                null,
                "Koliko je $1+1$?",
                "multiple_choice",
                "$2$",
                "Saberi jedinice.",
                3,
                1,
                1,
                [
                    new QuestionAuthoringOptionDto(null, "$2$", true, ContentFormat.LaTeX, RenderMode.Inline, "two"),
                    new QuestionAuthoringOptionDto(null, "$3$", false, ContentFormat.PlainText, RenderMode.Display, "three")
                ],
                [
                    new QuestionHintDto("formula", "$1+1$"),
                    new QuestionHintDto("clue", "Sabiranje."),
                    new QuestionHintDto("full", "Rezultat je 2.")
                ],
                [
                    new StepExplanationAuthoringDto(
                        1,
                        "Dodaj 1 i 1.",
                        "Pogledaj zbir.",
                        true,
                        ContentFormat.MarkdownWithMath,
                        ContentFormat.PlainText,
                        RenderMode.Auto,
                        RenderMode.Display,
                        "step-one-alt")
                ],
                "snapshot-seed",
                RequireSteps: true,
                TextFormat: ContentFormat.MarkdownWithMath,
                ExplanationFormat: ContentFormat.PlainText,
                HintFormat: ContentFormat.LaTeX,
                TextRenderMode: RenderMode.Inline,
                ExplanationRenderMode: RenderMode.Display,
                HintRenderMode: RenderMode.Auto,
                SemanticsAltText: "question-alt"),
            "author-1",
            CancellationToken.None);

        var question = await db.Questions
            .Include(x => x.Options)
            .ThenInclude(x => x.Translations)
            .Include(x => x.Steps)
            .ThenInclude(x => x.Translations)
            .Include(x => x.Translations)
            .SingleAsync(x => x.Id == create.Question.Id);

        question.SetHintDifficulty(2);
        question.SetPublishState(QuestionPublishStates.Published, "publisher-1", DateTime.UtcNow);
        question.SetCurrentVersionNumber(4);
        question.Translations.Add(new QuestionTranslation(
            question.Id,
            "sr",
            "Koliko je 1+1?",
            "Objasnjenje",
            "formula",
            "clue",
            "light",
            "medium",
            "full"));
        question.Options[0].Translations.Add(new OptionTranslation(question.Options[0].Id, "sr", "dva"));
        question.Steps[0].Translations.Add(new QuestionStepTranslation(question.Steps[0].Id, "sr", "Dodaj", "hint"));
        await db.SaveChangesAsync();

        var json = QuestionAuthoringService.CreateQuestionSnapshot(question);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(question.HintFormula, root.GetProperty("HintFormula").GetString());
        Assert.Equal(question.HintClue, root.GetProperty("HintClue").GetString());
        Assert.Equal(question.HintFull, root.GetProperty("HintFull").GetString());
        Assert.Equal(question.HintDifficulty, root.GetProperty("HintDifficulty").GetInt32());
        Assert.Equal(question.SemanticsAltText, root.GetProperty("SemanticsAltText").GetString());
        Assert.Equal(question.PublishState, root.GetProperty("PublishState").GetString());
        Assert.Equal(question.CurrentVersionNumber, root.GetProperty("CurrentVersionNumber").GetInt32());
        Assert.Equal(nameof(ContentFormat.MarkdownWithMath), root.GetProperty("TextFormat").GetString());
        Assert.Equal(nameof(RenderMode.Auto), root.GetProperty("HintRenderMode").GetString());
        Assert.False(root.GetProperty("IsDeleted").GetBoolean());

        var option = root.GetProperty("Options").EnumerateArray().First(x => x.GetProperty("IsCorrect").GetBoolean());
        Assert.Equal(nameof(ContentFormat.LaTeX), option.GetProperty("TextFormat").GetString());
        Assert.Equal(nameof(RenderMode.Inline), option.GetProperty("RenderMode").GetString());
        Assert.Equal("two", option.GetProperty("SemanticsAltText").GetString());
        Assert.Contains(option.GetProperty("Translations").EnumerateArray(), t => t.GetProperty("Lang").GetString() == "sr");

        var step = root.GetProperty("Steps").EnumerateArray().Single();
        Assert.Equal("step-one-alt", step.GetProperty("SemanticsAltText").GetString());
        Assert.Equal(nameof(ContentFormat.PlainText), step.GetProperty("HintFormat").GetString());
        Assert.Contains(step.GetProperty("Translations").EnumerateArray(), t => t.GetProperty("Text").GetString() == "Dodaj");

        var translation = root.GetProperty("Translations").EnumerateArray().Single();
        Assert.Equal("sr", translation.GetProperty("Lang").GetString());
        Assert.Equal("Objasnjenje", translation.GetProperty("Explanation").GetString());
        Assert.Equal("full", translation.GetProperty("HintFull").GetString());
    }

    [Fact]
    public async Task Revalidate_FailureDuringDraftCreate_LeavesNoPartialDraftPointerOrPreviewCache()
    {
        var dbName = $"authoring-revalidate-fail-{Guid.NewGuid():N}";
        await using (var seedDb = await TestDbContextFactory.CreateWithSeedAsync(dbName))
        {
            var shared = CreateSharedAuthoringService();
            var created = await shared.CreateQuestionAsync(
                seedDb,
                new QuestionAuthoringRequest(
                    null,
                    "Koliko je $2+2$?",
                    "multiple_choice",
                    "$4$",
                    "Saberi.",
                    2,
                    1,
                    1,
                    [
                        new QuestionAuthoringOptionDto(null, "$4$", true),
                        new QuestionAuthoringOptionDto(null, "$5$", false)
                    ],
                    [
                        new QuestionHintDto("formula", "$2+2$"),
                        new QuestionHintDto("clue", "Sabiranje."),
                        new QuestionHintDto("full", "4.")
                    ],
                    [new StepExplanationAuthoringDto(1, "Dodaj.", null, false)],
                    "revalidate-seed"),
                "author-1",
                CancellationToken.None);

            created.Question.SetPublishState(QuestionPublishStates.Published, "author-1", DateTime.UtcNow);
            created.Question.SetCurrentDraft(null);
            await seedDb.SaveChangesAsync();
        }

        var failure = new SaveFailureState { FailOnSaveCall = 1 };
        await using (var failingDb = CreateFailingDb(dbName, failure))
        {
            var service = CreateAuthoringService(failingDb);
            var questionId = await failingDb.Questions.Select(x => x.Id).OrderByDescending(x => x).FirstAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RevalidateAsync(questionId, "author-2", CancellationToken.None));
        }

        await using var assertDb = TestDbContextFactory.Create(dbName);
        var question = await assertDb.Questions.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Null(question.CurrentDraftId);
        Assert.Equal(0, await assertDb.QuestionDrafts.CountAsync(x => x.QuestionId == question.Id));
        Assert.Equal(0, await assertDb.QuestionValidationResults.CountAsync());
        Assert.Equal(0, await assertDb.QuestionPreviewCaches.CountAsync());
    }

    [Fact]
    public async Task Revalidate_WhenNoCurrentDraft_CreatesCompleteDraftValidationAndPreviewAtomically()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var shared = CreateSharedAuthoringService();
        var created = await shared.CreateQuestionAsync(
            db,
            new QuestionAuthoringRequest(
                null,
                "Koliko je $3+3$?",
                "multiple_choice",
                "$6$",
                "Saberi.",
                2,
                1,
                1,
                [
                    new QuestionAuthoringOptionDto(null, "$6$", true),
                    new QuestionAuthoringOptionDto(null, "$7$", false)
                ],
                [
                    new QuestionHintDto("formula", "$3+3$"),
                    new QuestionHintDto("clue", "Sabiranje."),
                    new QuestionHintDto("full", "6.")
                ],
                [new StepExplanationAuthoringDto(1, "Dodaj.", null, false)],
                "revalidate-ok"),
            "author-1",
            CancellationToken.None);

        created.Question.SetPublishState(QuestionPublishStates.Published, "author-1", DateTime.UtcNow);
        created.Question.SetCurrentDraft(null);
        await db.SaveChangesAsync();

        var service = CreateAuthoringService(db);
        var history = await service.RevalidateAsync(created.Question.Id, "author-2", CancellationToken.None);

        var question = await db.Questions.SingleAsync(x => x.Id == created.Question.Id);
        Assert.NotNull(question.CurrentDraftId);
        Assert.Equal(history.DraftId, question.CurrentDraftId);

        var draft = await db.QuestionDrafts.SingleAsync(x => x.Id == question.CurrentDraftId);
        Assert.NotNull(draft.LatestValidationResultId);
        Assert.True(await db.QuestionValidationResults.AnyAsync(x => x.Id == draft.LatestValidationResultId));
        Assert.True(await db.QuestionPreviewCaches.AnyAsync(x => x.DraftId == draft.Id));
    }

    private static ApiDbContext CreateFailingDb(string dbName, SaveFailureState failure)
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new SaveFailureApiDbContext(options, failure);
    }

    private static MathQuestionAuthoringService CreateAuthoringService(ApiDbContext db)
    {
        var cache = new HybridCacheService(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 }),
            NullLogger<HybridCacheService>.Instance);
        var sanitizer = new MathContentSanitizer();
        return new MathQuestionAuthoringService(
            db,
            NullLogger<MathQuestionAuthoringService>.Instance,
            cache,
            new MathContentLinter(),
            new LatexValidationService(),
            new MathNormalizationService(),
            new MathEquivalenceService(),
            new StepExplanationValidationService(),
            new DifficultyEstimationService(),
            new QuestionPreviewService(),
            new QuestionPublishGuardService(),
            new NoOpQuestionAutoHintGenerator(NullLogger<NoOpQuestionAutoHintGenerator>.Instance),
            CreateSharedAuthoringService(),
            sanitizer);
    }

    private static QuestionAuthoringService CreateSharedAuthoringService()
        => new(
            new MathContentSanitizer(),
            new QuestionAuthoringRequestValidator(),
            NullLogger<QuestionAuthoringService>.Instance);

    private sealed class SaveFailureState
    {
        private int saveCallCount;

        public int? FailOnSaveCall { get; set; }

        public bool ShouldThrowOnCurrentSave()
        {
            var callNumber = Interlocked.Increment(ref saveCallCount);
            return FailOnSaveCall == callNumber;
        }
    }

    private sealed class SaveFailureApiDbContext : ApiDbContext
    {
        private readonly SaveFailureState state;

        public SaveFailureApiDbContext(DbContextOptions<ApiDbContext> options, SaveFailureState state)
            : base(options)
        {
            this.state = state;
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (state.ShouldThrowOnCurrentSave())
                throw new InvalidOperationException("AUTHORING_DRAFT_SAVE_FAILURE");

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}

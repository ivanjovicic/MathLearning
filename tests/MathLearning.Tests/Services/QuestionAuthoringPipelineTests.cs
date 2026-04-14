using MathLearning.Application.Content;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Services;
using MathLearning.Domain.Enums;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Performance;
using MathLearning.Infrastructure.Services.QuestionAuthoring;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public class QuestionAuthoringPipelineTests
{
    [Fact]
    public void Linter_FlagsEmptyQuestion_DuplicateOptions_AndMissingSteps()
    {
        var linter = new MathContentLinter();
        var result = linter.Lint(new QuestionAuthoringRequest(
            null,
            " ",
            "multiple_choice",
            null,
            null,
            2,
            1,
            1,
            [
                new QuestionAuthoringOptionDto(null, "2", false),
                new QuestionAuthoringOptionDto(null, " 2 ", false)
            ],
            [],
            [],
            RequireSteps: true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.RuleId == "content.empty_question_text");
        Assert.Contains(result.Issues, x => x.RuleId == "options.duplicate_visual");
        Assert.Contains(result.Issues, x => x.RuleId == "steps.required_missing");
    }

    [Fact]
    public void LatexValidation_DetectsMalformedFraction()
    {
        var service = new LatexValidationService();
        var result = service.Validate(CreateValidRequest(correctAnswer: @"$\frac{1}{2}$", questionText: @"Koliko je $\frac{1}{?$"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Fields, x => x.ErrorCode == "latex.unbalanced_braces" || x.ErrorCode == "latex.invalid_fraction");
    }

    [Fact]
    public void Normalization_CanonicalizesSqrt_AndExponent()
    {
        var service = new MathNormalizationService();
        var result = service.Normalize(CreateValidRequest(
            correctAnswer: "$x^2$",
            questionText: "Reši $sqrt(x) + x^2$"));

        var textField = result.Fields.Single(x => x.FieldPath == "text");
        var answerField = result.Fields.Single(x => x.FieldPath == "correctAnswer");

        Assert.Contains(@"\sqrt{x}", textField.NormalizedValue);
        Assert.Contains("x^{2}", textField.NormalizedValue);
        Assert.Contains("x^{2}", answerField.NormalizedValue);
    }

    [Fact]
    public void Sanitizer_NormalizesDelimiters_And_RemovesScripts()
    {
        var sanitizer = new MathContentSanitizer();
        var value = sanitizer.NormalizeMathContent(@"Compute \(x^2\)<script>alert(1)</script>", ContentFormat.MarkdownWithMath);

        Assert.DoesNotContain("<script>", value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$x^2$", value);
    }

    [Fact]
    public async Task EquivalenceService_RecognizesNumeric_AndSymbolicEquivalence()
    {
        var service = new MathEquivalenceService();

        var numeric = await service.AreEquivalentAsync("1/2", "0.5", new MathEquivalenceContext(), CancellationToken.None);
        var symbolic = await service.AreEquivalentAsync("2(x+1)", "2x+2", new MathEquivalenceContext(), CancellationToken.None);

        Assert.True(numeric.IsEquivalent);
        Assert.True(symbolic.IsEquivalent);
    }

    [Fact]
    public async Task StepValidation_FlagsOrderingGap()
    {
        var service = new StepExplanationValidationService();
        var result = await service.ValidateAsync(CreateValidRequest(steps:
        [
            new StepExplanationAuthoringDto(1, "2x+4=10", null, false),
            new StepExplanationAuthoringDto(3, "x=3", null, true)
        ]), CancellationToken.None);

        Assert.Contains(result.SelectMany(x => x.Issues), x => x.RuleId == "steps.invalid_order");
    }

    [Fact]
    public async Task AuthoringService_SaveDraft_ThenPublish_CreatesPublishedQuestionVersion()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateAuthoringService(db);
        var save = await service.SaveDraftAsync(
            new SaveQuestionDraftRequest(CreateValidRequest(questionId: null)),
            "author-1",
            CancellationToken.None);

        var publish = await service.PublishAsync(
            new PublishQuestionRequest(save.DraftId, "initial publish"),
            "author-1",
            CancellationToken.None);

        Assert.True(save.Validation.Summary.CanPublish);
        Assert.True(publish.Published);
        Assert.True(publish.QuestionId > 0);
        Assert.Equal("published", publish.PublishState);
        Assert.Single(db.QuestionVersions.Where(x => x.QuestionId == publish.QuestionId));
        Assert.Equal(QuestionPublishStates.Published, db.Questions.Single(x => x.Id == publish.QuestionId).PublishState);
    }

    [Fact]
    public async Task AuthoringService_PersistsMathMetadata_OnPublish()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateAuthoringService(db);
        var request = CreateValidRequest(questionId: null) with
        {
            Text = "$1+1$",
            CorrectAnswer = @"$\frac{1}{2}$",
            TextFormat = ContentFormat.Latex,
            TextRenderMode = RenderMode.Display,
            SemanticsAltText = "one plus one equals two",
            Options =
            [
                new QuestionAuthoringOptionDto(1, @"$\frac{1}{2}$", true, ContentFormat.Latex, RenderMode.Inline, "one half"),
                new QuestionAuthoringOptionDto(2, @"$\frac{1}{3}$", false, ContentFormat.Latex, RenderMode.Inline, "one third")
            ],
            Steps =
            [
                new StepExplanationAuthoringDto(1, "$1+1=2$", null, true, ContentFormat.MarkdownWithMath, ContentFormat.MarkdownWithMath, RenderMode.Display, RenderMode.Auto, "step one")
            ]
        };

        var draft = await service.SaveDraftAsync(new SaveQuestionDraftRequest(request), "author-1", CancellationToken.None);
        var publish = await service.PublishAsync(new PublishQuestionRequest(draft.DraftId), "author-1", CancellationToken.None);

        var question = await db.Questions.Include(x => x.Options).Include(x => x.Steps).SingleAsync(x => x.Id == publish.QuestionId);

        Assert.Equal(ContentFormat.Latex, question.TextFormat);
        Assert.Equal(RenderMode.Display, question.TextRenderMode);
        Assert.Equal("one plus one equals two", question.SemanticsAltText);
        Assert.All(question.Options, option => Assert.Equal(ContentFormat.Latex, option.TextFormat));
        Assert.Equal("step one", question.Steps.Single().SemanticsAltText);
    }

    private static QuestionAuthoringRequest CreateValidRequest(
        int? questionId = 1,
        string questionText = "Koliko je $1+1$?",
        string correctAnswer = "$2$",
        IReadOnlyList<StepExplanationAuthoringDto>? steps = null)
        => new(
            questionId,
            questionText,
            "multiple_choice",
            correctAnswer,
            "Saberi brojeve.",
            2,
            1,
            1,
            [
                new QuestionAuthoringOptionDto(1, "$2$", true),
                new QuestionAuthoringOptionDto(2, "$3$", false),
                new QuestionAuthoringOptionDto(3, "$4$", false)
            ],
            [
                new QuestionHintDto("formula", "$a+b$"),
                new QuestionHintDto("clue", "Pogledaj sabiranje."),
                new QuestionHintDto("full", "Rezultat je 2.")
            ],
            steps ?? [new StepExplanationAuthoringDto(1, "$1+1=2$", null, false)],
            "test");

    private static MathQuestionAuthoringService CreateAuthoringService(ApiDbContext db)
    {
        var cache = new HybridCacheService(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 }),
            NullLogger<HybridCacheService>.Instance);

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
            new MathContentSanitizer());
    }
}

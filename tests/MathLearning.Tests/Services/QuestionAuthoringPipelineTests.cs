using MathLearning.Application.Content;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Services;
using MathLearning.Application.Validators;
using MathLearning.Domain.Enums;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Performance;
using MathLearning.Infrastructure.Services.QuestionAuthoring;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Collections.Concurrent;

namespace MathLearning.Tests.Services;

public class QuestionAuthoringPipelineTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
    public async Task EquivalenceService_NormalizeAnswerAsync_CanonicalizesOpenAnswerExpressions()
    {
        var service = new MathEquivalenceService();

        var normalized = await service.NormalizeAnswerAsync("sqrt(x) + x^2", CancellationToken.None);

        Assert.Equal(@"\sqrt{x}+x^{2}", normalized);
    }

    [Fact]
    public async Task EquivalenceService_UsesNumericComparison_ForOpenAnswerDecimals()
    {
        var service = new MathEquivalenceService();

        var equivalent = await service.AreEquivalentAsync(
            "1/2",
            "0.5",
            new MathEquivalenceContext(),
            CancellationToken.None);
        var notEquivalent = await service.AreEquivalentAsync(
            "1/2",
            "0.6",
            new MathEquivalenceContext(),
            CancellationToken.None);

        Assert.True(equivalent.IsEquivalent);
        Assert.Equal("numeric", equivalent.ComparisonMode);
        Assert.False(notEquivalent.IsEquivalent);
        Assert.Equal("numeric", notEquivalent.ComparisonMode);
    }

    [Fact]
    public async Task EquivalenceService_ValidateExpressionAsync_ReturnsExplicitValidationFailure()
    {
        var service = new MathEquivalenceService();

        var validation = await service.ValidateExpressionAsync(@"x+(", CancellationToken.None);

        Assert.False(validation.IsValid);
        Assert.Equal("math.invalid_expression", validation.ErrorCode);
        Assert.NotNull(validation.ErrorMessage);
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
            TextFormat = ContentFormat.LaTeX,
            TextRenderMode = RenderMode.Display,
            SemanticsAltText = "one plus one equals two",
            Options =
            [
                new QuestionAuthoringOptionDto(1, @"$\frac{1}{2}$", true, ContentFormat.LaTeX, RenderMode.Inline, "one half"),
                new QuestionAuthoringOptionDto(2, @"$\frac{1}{3}$", false, ContentFormat.LaTeX, RenderMode.Inline, "one third")
            ],
            Steps =
            [
                new StepExplanationAuthoringDto(1, "$1+1=2$", null, true, ContentFormat.MarkdownWithMath, ContentFormat.MarkdownWithMath, RenderMode.Display, RenderMode.Auto, "step one")
            ]
        };

        var draft = await service.SaveDraftAsync(new SaveQuestionDraftRequest(request), "author-1", CancellationToken.None);
        var publish = await service.PublishAsync(new PublishQuestionRequest(draft.DraftId), "author-1", CancellationToken.None);

        var question = await db.Questions.Include(x => x.Options).Include(x => x.Steps).SingleAsync(x => x.Id == publish.QuestionId);

        Assert.Equal(ContentFormat.LaTeX, question.TextFormat);
        Assert.Equal(RenderMode.Display, question.TextRenderMode);
        Assert.Equal("one plus one equals two", question.SemanticsAltText);
        Assert.All(question.Options, option => Assert.Equal(ContentFormat.LaTeX, option.TextFormat));
        Assert.Equal("step one", question.Steps.Single().SemanticsAltText);
    }

    [Fact]
    public async Task AuthoringService_Publish_SanitizesXssPayloadsBeforePersist()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateAuthoringService(db);
        var request = CreateValidRequest(questionId: null) with
        {
            Text = @"Koliko je $1+1$?<script>alert('xss')</script>",
            Explanation = @"<div onclick=""alert(1)"">Objasnjenje</div>",
            Hints =
            [
                new QuestionHintDto("formula", @"$a+b$<script>alert(2)</script>"),
                new QuestionHintDto("clue", @"<b>Pogledaj</b> sabiranje."),
                new QuestionHintDto("full", @"<script>alert(3)</script>Rezultat je 2.")
            ],
            Options =
            [
                new QuestionAuthoringOptionDto(1, @"$2$<script>alert(4)</script>", true),
                new QuestionAuthoringOptionDto(2, "$3$", false),
                new QuestionAuthoringOptionDto(3, "$4$", false)
            ],
            Steps =
            [
                new StepExplanationAuthoringDto(1, @"$1+1=2$<script>alert(5)</script>", "<img src=x onerror=alert(6)>", false)
            ]
        };

        var draft = await service.SaveDraftAsync(new SaveQuestionDraftRequest(request), "author-1", CancellationToken.None);
        var publish = await service.PublishAsync(new PublishQuestionRequest(draft.DraftId), "author-1", CancellationToken.None);
        var question = await db.Questions
            .Include(x => x.Options)
            .Include(x => x.Steps)
            .SingleAsync(x => x.Id == publish.QuestionId);

        Assert.DoesNotContain("<script", question.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", question.Explanation ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", question.HintFormula ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", question.HintClue ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", question.HintFull ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.All(question.Options, option =>
            Assert.DoesNotContain("<script", option.Text, StringComparison.OrdinalIgnoreCase));
        Assert.All(question.Steps, step =>
        {
            Assert.DoesNotContain("<script", step.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<script", step.Hint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task AuthoringService_SaveDraft_SanitizesProvidedSemanticsAltTextBeforePersist()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateAuthoringService(db);
        var request = CreateValidRequest(questionId: null) with
        {
            SemanticsAltText = @"<strong>Pitanje</strong><script>alert(1)</script>",
            Options =
            [
                new QuestionAuthoringOptionDto(1, "$2$", true, SemanticsAltText: "<em>Tacno</em><script>alert(2)</script>"),
                new QuestionAuthoringOptionDto(2, "$3$", false, SemanticsAltText: "<b>Netacno</b>"),
                new QuestionAuthoringOptionDto(3, "$4$", false, SemanticsAltText: "<img src=x onerror=alert(3)>Cetiri")
            ],
            Hints =
            [
                new QuestionHintDto("formula", "$a+b$", "<span>Formula</span><script>alert(4)</script>"),
                new QuestionHintDto("clue", "Pogledaj sabiranje.", "<b>Trag</b>"),
                new QuestionHintDto("full", "Rezultat je 2.", "<img src=x onerror=alert(5)>Pun hint")
            ],
            Steps =
            [
                new StepExplanationAuthoringDto(1, "$1+1=2$", null, false, SemanticsAltText: "<strong>Korak</strong><script>alert(6)</script>")
            ]
        };

        var save = await service.SaveDraftAsync(
            new SaveQuestionDraftRequest(request),
            "author-1",
            CancellationToken.None);

        var draft = await db.QuestionDrafts.SingleAsync(x => x.Id == save.DraftId);
        var stored = JsonSerializer.Deserialize<QuestionAuthoringRequest>(draft.ContentJson, JsonOptions);

        Assert.NotNull(stored);
        Assert.Equal("Pitanje", stored!.SemanticsAltText);
        Assert.All(stored.Options, option => Assert.DoesNotContain("<", option.SemanticsAltText ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        Assert.All(stored.Hints, hint => Assert.DoesNotContain("<", hint.SemanticsAltText ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        Assert.All(stored.Steps, step => Assert.DoesNotContain("<", step.SemanticsAltText ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Tacno", stored.Options[0].SemanticsAltText);
        Assert.Equal("Formula", stored.Hints[0].SemanticsAltText);
        Assert.Equal("Korak", stored.Steps[0].SemanticsAltText);
    }

    [Fact]
    public async Task AuthoringService_Validate_WarningOnlyPayload_ReturnsPassedWithWarnings_AndOrderedCollections()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateAuthoringService(db);
        var request = CreateValidRequest(questionId: null) with
        {
            Text = "Koliko je $1+1$?\n\n\n\nPazi na format.",
            Hints =
            [
                new QuestionHintDto("full", "Rezultat je 2."),
                new QuestionHintDto("clue", "Pogledaj sabiranje."),
                new QuestionHintDto("formula", "$a+b$")
            ]
        };

        var validation = await service.ValidateAsync(request, CancellationToken.None);
        var preview = await service.PreviewAsync(request, CancellationToken.None);

        Assert.True(validation.Summary.CanPublish);
        Assert.Equal(QuestionValidationStatuses.PassedWithWarnings, validation.Summary.Status);
        Assert.Equal(0, validation.Summary.ErrorCount);
        Assert.True(validation.Summary.WarningCount > 0);
        Assert.All(validation.Summary.Issues, issue =>
            Assert.Equal(ValidationIssueSeverities.Warning, issue.Severity));
        Assert.Contains(validation.Summary.Issues, issue =>
            issue.Stage == ValidationStageNames.Lint &&
            issue.RuleId == "content.suspicious_formatting");

        Assert.Equal(
            validation.Summary.Issues
                .OrderByDescending(x => string.Equals(x.Severity, ValidationIssueSeverities.Error, StringComparison.Ordinal))
                .ThenBy(x => x.Stage, StringComparer.Ordinal)
                .ThenBy(x => x.FieldPath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(x => x.RuleId, StringComparer.Ordinal)
                .ThenBy(x => x.Message, StringComparer.Ordinal)
                .Select(x => x.RuleId)
                .ToArray(),
            validation.Summary.Issues.Select(x => x.RuleId).ToArray());

        Assert.Equal(
            validation.Latex.Fields.OrderBy(x => x.FieldPath, StringComparer.Ordinal).Select(x => x.FieldPath).ToArray(),
            validation.Latex.Fields.Select(x => x.FieldPath).ToArray());
        Assert.Equal(
            validation.Normalized.Fields.OrderBy(x => x.FieldPath, StringComparer.Ordinal).Select(x => x.FieldPath).ToArray(),
            validation.Normalized.Fields.Select(x => x.FieldPath).ToArray());
        Assert.Equal(validation.Summary.Status, preview.Preview.Summary.Status);
        Assert.Equal(validation.Summary.CanPublish, preview.Preview.Summary.CanPublish);
        Assert.Equal(validation.Summary.ErrorCount, preview.Preview.Summary.ErrorCount);
        Assert.Equal(validation.Summary.WarningCount, preview.Preview.Summary.WarningCount);
        Assert.Equal(
            validation.Summary.Issues.Select(x => $"{x.Severity}:{x.Stage}:{x.FieldPath}:{x.RuleId}").ToArray(),
            preview.Preview.Summary.Issues.Select(x => $"{x.Severity}:{x.Stage}:{x.FieldPath}:{x.RuleId}").ToArray());
        Assert.Equal(
            preview.Preview.SafePreviewFields.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            preview.Preview.SafePreviewFields.Keys.ToArray());
    }

    [Fact]
    public async Task AuthoringService_Validate_MixedErrorAndWarningPayload_OrdersSummaryBySeverityStageAndField()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateAuthoringService(db);
        var request = CreateValidRequest(questionId: null) with
        {
            Text = "Koliko je $1+1$?\n\n\n\nPazi na format.",
            Explanation = @"Objasni $\frac{1}{?$",
            Steps =
            [
                new StepExplanationAuthoringDto(2, "x=4", null, false)
            ]
        };

        var validation = await service.ValidateAsync(request, CancellationToken.None);

        Assert.False(validation.Summary.CanPublish);
        Assert.Equal(QuestionValidationStatuses.Failed, validation.Summary.Status);
        Assert.True(validation.Summary.ErrorCount > 0);
        Assert.True(validation.Summary.WarningCount > 0);

        var orderedIssues = validation.Summary.Issues
            .OrderByDescending(x => string.Equals(x.Severity, ValidationIssueSeverities.Error, StringComparison.Ordinal))
            .ThenBy(x => x.Stage, StringComparer.Ordinal)
            .ThenBy(x => x.FieldPath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(x => x.RuleId, StringComparer.Ordinal)
            .ThenBy(x => x.Message, StringComparer.Ordinal)
            .Select(x => $"{x.Severity}:{x.Stage}:{x.FieldPath}:{x.RuleId}")
            .ToArray();

        Assert.Equal(
            orderedIssues,
            validation.Summary.Issues.Select(x => $"{x.Severity}:{x.Stage}:{x.FieldPath}:{x.RuleId}").ToArray());
        Assert.Equal(ValidationIssueSeverities.Error, validation.Summary.Issues[0].Severity);
        Assert.Contains(validation.Summary.Issues, issue => issue.RuleId == "content.suspicious_formatting");
        Assert.Contains(validation.Summary.Issues, issue => issue.RuleId is "latex.unbalanced_braces" or "latex.invalid_fraction");
        Assert.Contains(validation.Summary.Issues, issue => issue.RuleId == "steps.invalid_order");
    }

    [Fact]
    public async Task AuthoringService_Validate_OpenAnswerInvalidExpression_UsesValidationErrorRule()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateAuthoringService(db);
        var request = new QuestionAuthoringRequest(
            null,
            "Izracunaj vrednost izraza.",
            "open_answer",
            @"x+(",
            "Resi izraz.",
            2,
            1,
            1,
            [],
            [],
            [new StepExplanationAuthoringDto(1, "Pokreni racun.", null, false)],
            "open-answer-invalid");

        var validation = await service.ValidateAsync(request, CancellationToken.None);

        Assert.False(validation.Summary.CanPublish);
        Assert.Contains(validation.EquivalenceChecks, check => check.ComparisonMode == "validation" && !check.IsEquivalent);
        Assert.Contains(validation.Summary.Issues, issue =>
            issue.RuleId == "equivalence.validation_error" &&
            issue.Stage == ValidationStageNames.Equivalence &&
            issue.FieldPath == "correctAnswer");
        Assert.DoesNotContain(validation.Summary.Issues, issue => issue.RuleId == "equivalence.mismatch");
    }

    [Fact]
    public async Task AuthoringService_Validate_MultipleChoiceMismatch_KeepsEquivalenceMismatchRule()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateAuthoringService(db);
        var request = CreateValidRequest(questionId: null) with
        {
            CorrectAnswer = "$5$",
            Options =
            [
                new QuestionAuthoringOptionDto(1, "$2$", true),
                new QuestionAuthoringOptionDto(2, "$3$", false),
                new QuestionAuthoringOptionDto(3, "$4$", false)
            ]
        };

        var validation = await service.ValidateAsync(request, CancellationToken.None);

        Assert.False(validation.Summary.CanPublish);
        Assert.Contains(validation.EquivalenceChecks, check => !check.IsEquivalent);
        Assert.Contains(validation.Summary.Issues, issue =>
            issue.RuleId == "equivalence.mismatch" &&
            issue.Stage == ValidationStageNames.Equivalence);
    }

    [Fact]
    public async Task AuthoringService_ValidationWarnings_AreLoggedWithTopRuleIds()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var logger = new RecordingLogger<MathQuestionAuthoringService>();
        var service = CreateAuthoringService(db, logger);
        var request = CreateValidRequest(questionId: null) with
        {
            Text = "Koliko je $1+1$?\n\n\n\nPazi na format."
        };

        var validation = await service.ValidateAsync(request, CancellationToken.None);

        Assert.Equal(QuestionValidationStatuses.PassedWithWarnings, validation.Summary.Status);
        Assert.Contains(logger.Messages, message =>
            message.Contains("validation completed with warnings", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("content.suspicious_formatting", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AuthoringService_PublishBlocked_IsLoggedWithStatusAndRuleIds()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var logger = new RecordingLogger<MathQuestionAuthoringService>();
        var service = CreateAuthoringService(db, logger);
        var request = CreateValidRequest(questionId: null) with
        {
            Explanation = @"Objasni $\frac{1}{?$"
        };

        var save = await service.SaveDraftAsync(
            new SaveQuestionDraftRequest(request),
            "author-1",
            CancellationToken.None);
        var publish = await service.PublishAsync(
            new PublishQuestionRequest(save.DraftId, "blocked publish"),
            "author-1",
            CancellationToken.None);

        Assert.False(publish.Published);
        Assert.Contains(logger.Messages, message =>
            message.Contains("Publish blocked for draft", StringComparison.Ordinal) &&
            message.Contains("Status=failed", StringComparison.OrdinalIgnoreCase) &&
            (message.Contains("latex.unbalanced_braces", StringComparison.Ordinal) ||
             message.Contains("latex.invalid_fraction", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AuthoringService_UpdateQuestion_StepIdentityIsBoundToOrder_NotContentMove()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateSharedAuthoringService();

        var create = await service.CreateQuestionAsync(
            db,
            CreateValidRequest(questionId: null) with
            {
                Steps =
                [
                    new StepExplanationAuthoringDto(1, "Prvi tekst", null, false),
                    new StepExplanationAuthoringDto(2, "Drugi tekst", null, false)
                ]
            },
            "author-1",
            CancellationToken.None);

        var createdQuestion = await db.Questions
            .Include(x => x.Options)
            .Include(x => x.Steps)
            .SingleAsync(x => x.Id == create.Question.Id);

        var step1Id = createdQuestion.Steps.Single(x => x.StepIndex == 1).Id;
        var step2Id = createdQuestion.Steps.Single(x => x.StepIndex == 2).Id;

        await service.UpdateQuestionAsync(
            db,
            createdQuestion,
            new QuestionAuthoringRequest(
                createdQuestion.Id,
                createdQuestion.Text,
                "multiple_choice",
                createdQuestion.CorrectAnswer,
                createdQuestion.Explanation,
                createdQuestion.Difficulty,
                createdQuestion.CategoryId,
                createdQuestion.SubtopicId,
                createdQuestion.Options
                    .OrderBy(x => x.Order)
                    .Select(x => new QuestionAuthoringOptionDto(x.Id, x.Text, x.IsCorrect, x.TextFormat, x.RenderMode, x.SemanticsAltText))
                    .ToArray(),
                [
                    new QuestionHintDto("formula", createdQuestion.HintFormula ?? "$a+b$"),
                    new QuestionHintDto("clue", createdQuestion.HintClue ?? "Pogledaj sabiranje."),
                    new QuestionHintDto("full", createdQuestion.HintFull ?? "Rezultat je 2.")
                ],
                [
                    new StepExplanationAuthoringDto(1, "Drugi tekst", null, false),
                    new StepExplanationAuthoringDto(2, "Prvi tekst", null, false)
                ],
                "reorder-content"),
            "author-2",
            CancellationToken.None);

        var updatedQuestion = await db.Questions
            .Include(x => x.Steps)
            .SingleAsync(x => x.Id == createdQuestion.Id);

        Assert.Contains(updatedQuestion.Steps, step => step.Id == step1Id && step.StepIndex == 1 && step.Text == "Drugi tekst");
        Assert.Contains(updatedQuestion.Steps, step => step.Id == step2Id && step.StepIndex == 2 && step.Text == "Prvi tekst");
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

    private static MathQuestionAuthoringService CreateAuthoringService(
        ApiDbContext db,
        ILogger<MathQuestionAuthoringService>? logger = null)
    {
        var cache = new HybridCacheService(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 }),
            NullLogger<HybridCacheService>.Instance);
        var sanitizer = new MathContentSanitizer();
        var sharedAuthoringService = CreateSharedAuthoringService();

        return new MathQuestionAuthoringService(
            db,
            logger ?? NullLogger<MathQuestionAuthoringService>.Instance,
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
            sharedAuthoringService,
            sanitizer);
    }

    private static QuestionAuthoringService CreateSharedAuthoringService()
    {
        var sanitizer = new MathContentSanitizer();
        return new QuestionAuthoringService(
            sanitizer,
            new QuestionAuthoringRequestValidator(),
            NullLogger<QuestionAuthoringService>.Instance);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<string> messages = new();

        public IReadOnlyList<string> Messages => messages.ToArray();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}

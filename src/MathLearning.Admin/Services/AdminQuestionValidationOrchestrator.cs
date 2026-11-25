using MathLearning.Application.Content;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Services.QuestionAuthoring;

namespace MathLearning.Admin.Services;

/// <summary>
/// Runs the full question validation pipeline without requiring ApiDbContext.
/// All stage services are stateless — safe to use in the Admin project.
/// </summary>
public sealed class AdminQuestionValidationOrchestrator
{
    private readonly IMathContentLinter linter;
    private readonly ILatexValidationService latexValidation;
    private readonly IMathNormalizationService normalization;
    private readonly IMathEquivalenceService equivalence;
    private readonly IStepExplanationValidationService stepValidation;
    private readonly IDifficultyEstimationService difficulty;
    private readonly IQuestionPreviewService previewService;
    private readonly IQuestionPublishGuardService publishGuard;
    private readonly IMathContentSanitizer sanitizer;
    private readonly MathVisualContentPolicy mathVisualPolicy;

    public AdminQuestionValidationOrchestrator(
        IMathContentLinter linter,
        ILatexValidationService latexValidation,
        IMathNormalizationService normalization,
        IMathEquivalenceService equivalence,
        IStepExplanationValidationService stepValidation,
        IDifficultyEstimationService difficulty,
        IQuestionPreviewService previewService,
        IQuestionPublishGuardService publishGuard,
        IMathContentSanitizer sanitizer,
        MathVisualContentPolicy mathVisualPolicy)
    {
        this.linter = linter;
        this.latexValidation = latexValidation;
        this.normalization = normalization;
        this.equivalence = equivalence;
        this.stepValidation = stepValidation;
        this.difficulty = difficulty;
        this.previewService = previewService;
        this.publishGuard = publishGuard;
        this.sanitizer = sanitizer;
        this.mathVisualPolicy = mathVisualPolicy;
    }

    public async Task<ValidateQuestionResponse> ValidateAsync(
        QuestionAuthoringRequest request,
        CancellationToken cancellationToken = default)
    {
        var sanitized = SanitizeRequest(request);

        var lint = linter.Lint(sanitized);
        var mathVisualIssues = mathVisualPolicy.Validate(
            sanitized,
            MathVisualContentPolicy.ComputeContentHash(sanitized));
        if (mathVisualIssues.Count > 0)
        {
            lint = new ContentLintResultDto(
                lint.IsValid && mathVisualIssues.All(x => x.Severity != ValidationIssueSeverities.Error),
                lint.Issues.Concat(mathVisualIssues).ToArray());
        }
        var latex = latexValidation.Validate(sanitized);
        var normalized = normalization.Normalize(sanitized);
        var equivalenceChecks = await BuildEquivalenceChecksAsync(sanitized, cancellationToken);
        var stepResults = await stepValidation.ValidateAsync(sanitized, cancellationToken);
        var difficultyResult = difficulty.Estimate(sanitized, normalized);
        var summary = publishGuard.BuildSummary(lint, latex, equivalenceChecks, stepResults);

        return new ValidateQuestionResponse(
            summary,
            lint,
            latex,
            normalized,
            equivalenceChecks,
            stepResults,
            difficultyResult);
    }

    private QuestionAuthoringRequest SanitizeRequest(QuestionAuthoringRequest request)
    {
        return QuestionAuthoringRequestSanitizer.Sanitize(request, sanitizer);
    }

    private async Task<IReadOnlyList<EquivalentAnswerResultDto>> BuildEquivalenceChecksAsync(
        QuestionAuthoringRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CorrectAnswer))
        {
            return Array.Empty<EquivalentAnswerResultDto>();
        }

        var correctOptions = request.Options.Where(x => x.IsCorrect).ToArray();
        if (correctOptions.Length == 0)
        {
            return Array.Empty<EquivalentAnswerResultDto>();
        }

        var ctx = new MathEquivalenceContext();
        var tasks = correctOptions.Select(opt =>
            equivalence.AreEquivalentAsync(request.CorrectAnswer, opt.Text, ctx, cancellationToken));

        return await Task.WhenAll(tasks);
    }
}

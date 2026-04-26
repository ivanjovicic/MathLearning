using MathLearning.Application.Content;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Services;
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

    public AdminQuestionValidationOrchestrator(
        IMathContentLinter linter,
        ILatexValidationService latexValidation,
        IMathNormalizationService normalization,
        IMathEquivalenceService equivalence,
        IStepExplanationValidationService stepValidation,
        IDifficultyEstimationService difficulty,
        IQuestionPreviewService previewService,
        IQuestionPublishGuardService publishGuard,
        IMathContentSanitizer sanitizer)
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
    }

    public async Task<ValidateQuestionResponse> ValidateAsync(
        QuestionAuthoringRequest request,
        CancellationToken cancellationToken = default)
    {
        var sanitized = SanitizeRequest(request);

        var lint = linter.Lint(sanitized);
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
        return request with
        {
            Text = sanitizer.NormalizeMathContent(request.Text, request.TextFormat),
            Explanation = string.IsNullOrWhiteSpace(request.Explanation)
                ? request.Explanation
                : sanitizer.NormalizeMathContent(request.Explanation, request.ExplanationFormat),
            Options = request.Options
                .Select(o => o with { Text = sanitizer.NormalizeMathContent(o.Text, o.TextFormat) })
                .ToList(),
            Steps = request.Steps
                .Select(s => s with
                {
                    Text = sanitizer.NormalizeMathContent(s.Text, s.TextFormat),
                    Hint = string.IsNullOrWhiteSpace(s.Hint)
                        ? s.Hint
                        : sanitizer.NormalizeMathContent(s.Hint, s.HintFormat)
                })
                .ToList()
        };
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

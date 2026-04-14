using MathLearning.Application.DTOs.Questions;

namespace MathLearning.Application.Services;

public interface IMathQuestionAuthoringService
{
    Task<ValidateQuestionResponse> ValidateAsync(QuestionAuthoringRequest request, CancellationToken cancellationToken);
    Task<PreviewQuestionResponse> PreviewAsync(QuestionAuthoringRequest request, CancellationToken cancellationToken);
    Task<SaveQuestionDraftResponse> SaveDraftAsync(
        SaveQuestionDraftRequest request,
        string? actorUserId,
        CancellationToken cancellationToken);
    Task<PublishQuestionResponse> PublishAsync(
        PublishQuestionRequest request,
        string? actorUserId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<QuestionVersionHistoryItemDto>> GetVersionsAsync(int questionId, CancellationToken cancellationToken);
    Task<QuestionValidationHistoryDto?> GetValidationAsync(int questionId, CancellationToken cancellationToken);
    Task<QuestionValidationHistoryDto> RevalidateAsync(int questionId, string? actorUserId, CancellationToken cancellationToken);
}

public interface IMathQuestionValidationService
{
    Task<ValidateQuestionResponse> ValidateAsync(QuestionAuthoringRequest request, CancellationToken cancellationToken);
}

public interface IMathContentLinter
{
    ContentLintResultDto Lint(QuestionAuthoringRequest request);
}

public interface ILatexValidationService
{
    LatexValidationResultDto Validate(QuestionAuthoringRequest request);
}

public interface IMathNormalizationService
{
    QuestionNormalizationResultDto Normalize(QuestionAuthoringRequest request);
}

public sealed record MathEquivalenceContext(
    double NumericTolerance = 1e-9,
    bool AllowApproximateNumericComparison = true,
    bool AllowSymbolicComparison = true);

public interface IMathEquivalenceService
{
    Task<EquivalentAnswerResultDto> AreEquivalentAsync(
        string expected,
        string actual,
        MathEquivalenceContext context,
        CancellationToken cancellationToken);
    Task<string?> NormalizeAnswerAsync(string expression, CancellationToken cancellationToken);
    Task<MathExpressionValidationResultDto> ValidateExpressionAsync(string expression, CancellationToken cancellationToken);
}

public interface IStepExplanationValidationService
{
    Task<IReadOnlyList<StepValidationResultDto>> ValidateAsync(
        QuestionAuthoringRequest request,
        CancellationToken cancellationToken);
}

public interface IDifficultyEstimationService
{
    DifficultyEstimateResultDto Estimate(QuestionAuthoringRequest request, QuestionNormalizationResultDto normalizationResult);
}

public interface IQuestionPreviewService
{
    QuestionPreviewPayloadDto BuildPreview(
        QuestionAuthoringRequest request,
        ContentLintResultDto lint,
        LatexValidationResultDto latex,
        QuestionNormalizationResultDto normalized,
        IReadOnlyList<EquivalentAnswerResultDto> equivalenceChecks,
        IReadOnlyList<StepValidationResultDto> stepResults,
        DifficultyEstimateResultDto difficulty,
        QuestionValidationSummaryDto summary);
}

public interface IQuestionVersioningService
{
    Task<IReadOnlyList<QuestionVersionHistoryItemDto>> GetVersionsAsync(int questionId, CancellationToken cancellationToken);
}

public interface IQuestionPublishGuardService
{
    QuestionValidationSummaryDto BuildSummary(
        ContentLintResultDto lint,
        LatexValidationResultDto latex,
        IReadOnlyList<EquivalentAnswerResultDto> equivalenceChecks,
        IReadOnlyList<StepValidationResultDto> stepResults);
}

public interface IQuestionAutoHintGenerator
{
    Task<IReadOnlyList<QuestionHintDto>> GenerateAsync(
        QuestionAuthoringRequest request,
        CancellationToken cancellationToken);
}

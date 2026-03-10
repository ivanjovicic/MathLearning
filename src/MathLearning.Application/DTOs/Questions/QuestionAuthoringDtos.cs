using MathLearning.Domain.Enums;

namespace MathLearning.Application.DTOs.Questions;

public record QuestionAuthoringOptionDto(
    int? Id,
    string Text,
    bool IsCorrect,
    ContentFormat TextFormat = ContentFormat.MarkdownWithMath,
    RenderMode RenderMode = RenderMode.Auto,
    string? SemanticsAltText = null
);

public record QuestionHintDto(
    string Key,
    string Text,
    string? SemanticsAltText = null
);

public record StepExplanationAuthoringDto(
    int Order,
    string Text,
    string? Hint,
    bool Highlight,
    ContentFormat TextFormat = ContentFormat.MarkdownWithMath,
    ContentFormat HintFormat = ContentFormat.MarkdownWithMath,
    RenderMode TextRenderMode = RenderMode.Auto,
    RenderMode HintRenderMode = RenderMode.Auto,
    string? SemanticsAltText = null
);

public record QuestionAuthoringRequest(
    int? QuestionId,
    string Text,
    string Type,
    string? CorrectAnswer,
    string? Explanation,
    int Difficulty,
    int CategoryId,
    int SubtopicId,
    IReadOnlyList<QuestionAuthoringOptionDto> Options,
    IReadOnlyList<QuestionHintDto> Hints,
    IReadOnlyList<StepExplanationAuthoringDto> Steps,
    string? ChangeReason = null,
    bool RequireSteps = false,
    ContentFormat TextFormat = ContentFormat.MarkdownWithMath,
    ContentFormat ExplanationFormat = ContentFormat.MarkdownWithMath,
    ContentFormat HintFormat = ContentFormat.MarkdownWithMath,
    RenderMode TextRenderMode = RenderMode.Auto,
    RenderMode ExplanationRenderMode = RenderMode.Auto,
    RenderMode HintRenderMode = RenderMode.Auto,
    string? SemanticsAltText = null
);

public record ValidationIssueDto(
    string Stage,
    string Severity,
    string RuleId,
    string Message,
    string? FieldPath = null,
    string? Suggestion = null
);

public record ContentLintResultDto(
    bool IsValid,
    IReadOnlyList<ValidationIssueDto> Issues
);

public record LatexValidationDetailDto(
    string FieldPath,
    bool IsValid,
    string? NormalizedExpression,
    string? ErrorCode,
    string? ErrorMessage,
    string? SafeFallbackText
);

public record LatexValidationResultDto(
    bool IsValid,
    IReadOnlyList<LatexValidationDetailDto> Fields
);

public record ContentSegmentDto(
    string Kind,
    string Value
);

public record NormalizedContentFieldDto(
    string FieldPath,
    string RawValue,
    string NormalizedValue,
    IReadOnlyList<ContentSegmentDto> Segments
);

public record QuestionNormalizationResultDto(
    bool IsSafe,
    IReadOnlyList<NormalizedContentFieldDto> Fields
);

public record MathExpressionValidationResultDto(
    bool IsValid,
    string Expression,
    string? NormalizedExpression,
    string? ErrorCode,
    string? ErrorMessage
);

public record EquivalentAnswerResultDto(
    bool IsEquivalent,
    string Expected,
    string Actual,
    string ComparisonMode,
    string? Reason = null
);

public record StepValidationResultDto(
    int Order,
    bool IsValid,
    bool FollowsPrevious,
    IReadOnlyList<ValidationIssueDto> Issues
);

public record DifficultyEstimateResultDto(
    int Score,
    string Band,
    IReadOnlyList<string> Signals
);

public record QuestionValidationSummaryDto(
    bool CanPublish,
    string Status,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<ValidationIssueDto> Issues
);

public record QuestionPreviewPayloadDto(
    QuestionAuthoringRequest Raw,
    QuestionNormalizationResultDto Normalized,
    LatexValidationResultDto Latex,
    ContentLintResultDto Lint,
    IReadOnlyList<EquivalentAnswerResultDto> EquivalenceChecks,
    IReadOnlyList<StepValidationResultDto> Steps,
    DifficultyEstimateResultDto Difficulty,
    QuestionValidationSummaryDto Summary,
    IReadOnlyDictionary<string, string?> SafePreviewFields
);

public record ValidateQuestionResponse(
    QuestionValidationSummaryDto Summary,
    ContentLintResultDto Lint,
    LatexValidationResultDto Latex,
    QuestionNormalizationResultDto Normalized,
    IReadOnlyList<EquivalentAnswerResultDto> EquivalenceChecks,
    IReadOnlyList<StepValidationResultDto> StepResults,
    DifficultyEstimateResultDto Difficulty
);

public record PreviewQuestionResponse(
    QuestionPreviewPayloadDto Preview
);

public record SaveQuestionDraftRequest(
    QuestionAuthoringRequest Content,
    string? ChangeReason = null
);

public record SaveQuestionDraftResponse(
    Guid DraftId,
    int DraftVersion,
    string PublishState,
    ValidateQuestionResponse Validation
);

public record PublishQuestionRequest(
    Guid DraftId,
    string? ChangeReason = null
);

public record PublishQuestionResponse(
    bool Published,
    int QuestionId,
    long VersionId,
    int VersionNumber,
    string PublishState,
    QuestionValidationSummaryDto ValidationSummary
);

public record QuestionVersionHistoryItemDto(
    long VersionId,
    int VersionNumber,
    string PublishState,
    string? AuthorUserId,
    string? EditorUserId,
    string? ChangeReason,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc,
    long? PreviousVersionId
);

public record QuestionValidationHistoryDto(
    Guid ValidationResultId,
    Guid DraftId,
    string Status,
    bool HasErrors,
    bool HasWarnings,
    int IssueCount,
    DateTime ValidatedAtUtc,
    IReadOnlyList<ValidationIssueDto> Issues
);

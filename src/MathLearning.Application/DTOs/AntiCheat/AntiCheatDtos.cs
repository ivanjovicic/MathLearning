namespace MathLearning.Application.DTOs.AntiCheat;

public sealed record AntiCheatAnswerObservationInput(
    string UserId,
    string SourceType,
    int QuestionId,
    int? TopicId,
    int? SubtopicId,
    Guid? SessionId,
    string? DeviceId,
    long? ClientSequence,
    string? Answer,
    bool IsCorrect,
    int ResponseTimeMs,
    double? Confidence,
    DateTime AnsweredAtUtc,
    string? MetadataJson = null);

public sealed record AntiCheatMlPromptDto(
    string PromptVersion,
    string SystemPrompt,
    string UserPrompt,
    string PayloadJson);

public sealed record AntiCheatDetectionResultDto(
    bool IsSuspicious,
    int RiskScore,
    string Severity,
    string Decision,
    string ReasonSummary,
    IReadOnlyList<string> Signals,
    AntiCheatMlPromptDto Prompt);

public sealed record AntiCheatOverviewDto(
    long PendingCount,
    long HighSeverityCount,
    long CriticalSeverityCount,
    long ConfirmedCount,
    long FalsePositiveCount,
    long Last24HoursCount,
    long MlQueuedCount,
    long MlFailedCount,
    long MlCompletedCount);

public sealed record AntiCheatMlReviewResultDto(
    string Classification,
    double Confidence,
    string RecommendedAction,
    string Summary,
    string ModelName,
    string OutputJson);

public sealed record AntiCheatDetectionItemDto(
    Guid Id,
    string UserId,
    string SourceType,
    int QuestionId,
    int? TopicId,
    int? SubtopicId,
    Guid? SessionId,
    string? DeviceId,
    long? ClientSequence,
    DateTime AnsweredAtUtc,
    int ResponseTimeMs,
    bool IsCorrect,
    double? Confidence,
    int RiskScore,
    string Severity,
    string Decision,
    string ReasonSummary,
    string ReviewStatus,
    DateTime DetectedAtUtc,
    DateTime? ReviewedAtUtc,
    string SignalsJson,
    string PromptVersion,
    string MlReviewStatus,
    int MlReviewAttempts,
    string? MlModelName,
    DateTime? MlReviewedAtUtc,
    string? MlLastError,
    string? MlReviewOutputJson);

public sealed record ReviewAntiCheatDetectionRequest(
    string ReviewStatus,
    string? Notes = null);

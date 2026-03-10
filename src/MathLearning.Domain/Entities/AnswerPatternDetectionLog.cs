namespace MathLearning.Domain.Entities;

public static class AntiCheatDetectionSeverities
{
    public const string Medium = "medium";
    public const string High = "high";
    public const string Critical = "critical";
}

public static class AntiCheatDetectionDecisions
{
    public const string Review = "review";
    public const string LikelyBot = "likely_bot";
}

public static class AntiCheatReviewStatuses
{
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string FalsePositive = "false_positive";
    public const string Ignored = "ignored";
}

public static class AntiCheatMlReviewStatuses
{
    public const string NotRequested = "not_requested";
    public const string Queued = "queued";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

/// <summary>
/// Immutable audit record for suspicious answer behavior.
/// Raw answer content is intentionally not stored; only a fingerprint and derived signals are persisted.
/// </summary>
public sealed class AnswerPatternDetectionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public int QuestionId { get; set; }
    public int? TopicId { get; set; }
    public int? SubtopicId { get; set; }
    public Guid? SessionId { get; set; }
    public string? DeviceId { get; set; }
    public long? ClientSequence { get; set; }
    public DateTime AnsweredAtUtc { get; set; }
    public int ResponseTimeMs { get; set; }
    public bool IsCorrect { get; set; }
    public double? Confidence { get; set; }
    public int AnswerLength { get; set; }
    public string? AnswerFingerprint { get; set; }
    public int RiskScore { get; set; }
    public string Severity { get; set; } = AntiCheatDetectionSeverities.Medium;
    public string Decision { get; set; } = AntiCheatDetectionDecisions.Review;
    public string ReasonSummary { get; set; } = string.Empty;
    public string SignalsJson { get; set; } = "[]";
    public string PromptVersion { get; set; } = string.Empty;
    public string PromptPayloadJson { get; set; } = "{}";
    public string DetectionEngine { get; set; } = "heuristic_answer_pattern_v1";
    public string ReviewStatus { get; set; } = AntiCheatReviewStatuses.Pending;
    public string? ReviewedByUserId { get; set; }
    public string? ReviewNotes { get; set; }
    public string MlReviewStatus { get; set; } = AntiCheatMlReviewStatuses.NotRequested;
    public int MlReviewAttempts { get; set; }
    public string? MlModelName { get; set; }
    public string? MlReviewOutputJson { get; set; }
    public string? MlLastError { get; set; }
    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? MlLastAttemptAtUtc { get; set; }
    public DateTime? MlReviewedAtUtc { get; set; }
}

namespace MathLearning.Domain.Entities;

public static class QuestionPublishStates
{
    public const string Draft = "draft";
    public const string Validated = "validated";
    public const string Published = "published";
    public const string Archived = "archived";
}

public static class QuestionValidationStatuses
{
    public const string Pending = "pending";
    public const string Passed = "passed";
    public const string PassedWithWarnings = "passed_with_warnings";
    public const string Failed = "failed";
}

public static class ValidationIssueSeverities
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
}

public static class ValidationStageNames
{
    public const string Intake = "intake";
    public const string Lint = "lint";
    public const string Latex = "latex";
    public const string Normalization = "normalization";
    public const string Equivalence = "equivalence";
    public const string Steps = "steps";
    public const string Difficulty = "difficulty";
    public const string Preview = "preview";
    public const string PublishGuard = "publish_guard";
}

public class QuestionDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? QuestionId { get; set; }
    public Guid? PreviousDraftId { get; set; }
    public int DraftVersion { get; set; } = 1;
    public string ContentJson { get; set; } = "{}";
    public string NormalizedContentJson { get; set; } = "{}";
    public string ContentHash { get; set; } = string.Empty;
    public string PublishState { get; set; } = QuestionPublishStates.Draft;
    public string ValidationStatus { get; set; } = QuestionValidationStatuses.Pending;
    public string? ChangeReason { get; set; }
    public string? AuthorUserId { get; set; }
    public string? EditorUserId { get; set; }
    public Guid? LatestValidationResultId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Question? Question { get; set; }
    public QuestionValidationResult? LatestValidationResult { get; set; }
}

public class QuestionVersion
{
    public long Id { get; set; }
    public int QuestionId { get; set; }
    public Guid SourceDraftId { get; set; }
    public long? PreviousVersionId { get; set; }
    public int VersionNumber { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public string NormalizedSnapshotJson { get; set; } = "{}";
    public string PublishState { get; set; } = QuestionPublishStates.Draft;
    public string? ChangeReason { get; set; }
    public string? AuthorUserId { get; set; }
    public string? EditorUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }

    public Question? Question { get; set; }
    public QuestionDraft? SourceDraft { get; set; }
    public QuestionVersion? PreviousVersion { get; set; }
}

public class QuestionValidationResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DraftId { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string Status { get; set; } = QuestionValidationStatuses.Pending;
    public bool HasErrors { get; set; }
    public bool HasWarnings { get; set; }
    public int IssueCount { get; set; }
    public string? SummaryJson { get; set; }
    public string? PreviewPayloadJson { get; set; }
    public DateTime ValidatedAtUtc { get; set; } = DateTime.UtcNow;

    public QuestionDraft? Draft { get; set; }
    public List<QuestionValidationIssue> Issues { get; set; } = new();
}

public class QuestionValidationIssue
{
    public long Id { get; set; }
    public Guid ValidationResultId { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string Severity { get; set; } = ValidationIssueSeverities.Info;
    public string Message { get; set; } = string.Empty;
    public string? FieldPath { get; set; }
    public string? Suggestion { get; set; }
    public string? MetadataJson { get; set; }

    public QuestionValidationResult? ValidationResult { get; set; }
}

public class QuestionPreviewCache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DraftId { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string PreviewPayloadJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(7);

    public QuestionDraft? Draft { get; set; }
}

public class QuestionAuthoringAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DraftId { get; set; }
    public int? QuestionId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? ActorUserId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public QuestionDraft? Draft { get; set; }
    public Question? Question { get; set; }
}

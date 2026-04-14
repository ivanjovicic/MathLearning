using System.Text.Json;
using MathLearning.Domain.Enums;

namespace MathLearning.Application.DTOs.Sync;

public sealed record RegisterSyncDeviceRequest(
    string DeviceId,
    string? DeviceName,
    string Platform,
    string? AppVersion);

public sealed record RegisterSyncDeviceResponse(
    string DeviceId,
    string DeviceSecret,
    DateTime RegisteredAtUtc);

public sealed record SyncOperationDto(
    Guid OperationId,
    string DeviceId,
    string UserId,
    long ClientSequence,
    string OperationType,
    DateTime OccurredAtUtc,
    JsonElement Payload,
    string? Signature);

public sealed record SyncRequestDto(
    string DeviceId,
    long LastKnownServerEvent,
    IReadOnlyList<SyncOperationDto> Operations,
    string? BundleVersion = null);

public sealed record SyncOperationAckDto(
    Guid OperationId,
    long ClientSequence,
    string Status,
    string? ErrorCode = null,
    string? Message = null);

public sealed record SyncServerEventDto(
    long Id,
    string EventType,
    string AggregateType,
    string AggregateId,
    Guid? SourceOperationId,
    JsonElement Payload,
    DateTime CreatedAtUtc);

public sealed record SyncResponseDto(
    IReadOnlyList<SyncOperationAckDto> AcknowledgedOperations,
    IReadOnlyList<SyncServerEventDto> ServerOperations,
    long NewCheckpoint,
    int RecommendedBackoffSeconds);

public sealed record SyncMetricsSnapshotDto(
    long SyncRequests,
    long ProcessedOperations,
    long DuplicateOperations,
    long RejectedOperations,
    long FailedOperations,
    long DeadLetterOperations,
    IReadOnlyDictionary<string, long> FailuresByCode);

public sealed record SyncAdminOverviewDto(
    long ActiveDevices24h,
    long PendingDeadLetters,
    long ExhaustedDeadLetters,
    long FailedOperationsInLog,
    long LatestServerEventId,
    SyncMetricsSnapshotDto Metrics);

public sealed record SyncDeadLetterItemDto(
    Guid OperationId,
    long? SyncEventLogId,
    string DeviceId,
    string UserId,
    string OperationType,
    string Status,
    int RetryCount,
    string FailureReason,
    DateTime CreatedAtUtc,
    DateTime LastFailedAtUtc,
    DateTime? LastRedriveAttemptAtUtc,
    DateTime? ResolvedAtUtc,
    string? ResolutionNote);

public sealed record SyncDeviceAdminDto(
    string DeviceId,
    string UserId,
    string? DeviceName,
    string Platform,
    string? AppVersion,
    string Status,
    DateTime LastSeenAtUtc,
    DateTime? LastSyncTimeUtc,
    long LastProcessedClientSequence,
    long LastAcknowledgedEvent,
    string? LastBundleVersion);

public sealed record SyncDeadLetterRedriveResponseDto(
    Guid OperationId,
    string Status,
    int RetryCount,
    string? ErrorCode,
    string? Message,
    DateTime? ResolvedAtUtc);

public sealed record SyncDeadLetterRedriveBatchResponseDto(
    int Attempted,
    int Succeeded,
    int Failed,
    int Exhausted,
    IReadOnlyList<SyncDeadLetterRedriveResponseDto> Results);

public sealed record RedriveSyncDeadLettersRequest(
    int? Take = null,
    bool IncludeExhausted = false);

public sealed record SubmitAnswerSyncPayloadDto(
    string SessionId,
    int QuestionId,
    string Answer,
    int TimeSpentSeconds,
    DateTime AnsweredAtUtc,
    bool? IsCorrectOffline = null);

public sealed record SyncBundleQuestionDto(
    int Id,
    string Type,
    string Text,
    int Difficulty,
    IReadOnlyList<SyncBundleOptionDto> Options,
    string? HintLight,
    string? HintMedium,
    string? HintFull,
    string? Explanation,
    ContentFormat TextFormat = ContentFormat.MarkdownWithMath,
    ContentFormat ExplanationFormat = ContentFormat.MarkdownWithMath,
    ContentFormat HintFormat = ContentFormat.MarkdownWithMath,
    RenderMode TextRenderMode = RenderMode.Auto,
    RenderMode ExplanationRenderMode = RenderMode.Auto,
    RenderMode HintRenderMode = RenderMode.Auto,
    string? SemanticsAltText = null);

public sealed record SyncBundleOptionDto(
    int Id,
    string Text,
    ContentFormat TextFormat = ContentFormat.MarkdownWithMath,
    RenderMode RenderMode = RenderMode.Auto,
    string? SemanticsAltText = null);

public sealed record OfflineBundleManifestDto(
    string Version,
    DateTime GeneratedAtUtc,
    int QuestionCount,
    int TopicCount,
    int SubtopicCount);

public sealed record OfflineBundleResponseDto(
    OfflineBundleManifestDto Manifest,
    IReadOnlyList<SyncBundleQuestionDto> Questions,
    IReadOnlyList<OfflineBundleTopicDto> Topics,
    IReadOnlyList<OfflineBundleSubtopicDto> Subtopics,
    IReadOnlyList<int> QuizSequence,
    OfflineBundleUserSnapshotDto UserSnapshot);

public sealed record OfflineBundleTopicDto(
    int Id,
    string Name,
    string? Description);

public sealed record OfflineBundleSubtopicDto(
    int Id,
    int TopicId,
    string Name);

public sealed record OfflineBundleUserSnapshotDto(
    int Xp,
    int Level,
    int Streak,
    IReadOnlyList<OfflineBundleQuestionProgressDto> QuestionProgress);

public sealed record OfflineBundleQuestionProgressDto(
    int QuestionId,
    int Attempts,
    int CorrectAttempts,
    DateTime? LastAttemptAt,
    DateTime? NextReviewAt);

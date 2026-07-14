namespace MathLearning.Application.DTOs.Progress;

public sealed record ProgressSyncRequestDto(
    string? OperationId,
    string? IdempotencyKey,
    string? DeviceId,
    DateOnly? Day,
    bool? Completed,
    IReadOnlyList<Guid>? QuizOperationIds,
    IReadOnlyList<Guid>? PracticeSessionIds);

public sealed record ProgressSyncResponseDto(
    DateOnly Day,
    bool Completed,
    int SettledEvidenceCount,
    int DailyStreak,
    int StreakFreezeCount,
    bool AlreadyProcessed = false);

public sealed record ProgressSyncCompatibilityResponse(
    string ErrorCode,
    string Message,
    string RequiredVersion);

public sealed record ProgressSyncIdempotencyConflictResponse(
    bool AlreadyProcessed,
    bool Conflict,
    string ErrorCode,
    string OperationId,
    string IdempotencyKey);

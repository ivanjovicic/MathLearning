namespace MathLearning.Domain.Events;

public sealed record AdaptiveAnswerLegacySrsSyncRequested(
    string UserId,
    int QuestionId,
    bool IsCorrect,
    int ResponseTimeSeconds,
    Guid AdaptiveSessionId,
    Guid AdaptiveSessionItemId) : DomainEventBase;

namespace MathLearning.Domain.Events;

public sealed record QuizAttemptIngestRequested(
    string UserId,
    string AttemptKey,
    Guid QuizId,
    int QuestionId,
    int SubtopicId,
    bool Correct,
    int TimeSpentMs,
    DateTime CreatedAtUtc
) : DomainEventBase;

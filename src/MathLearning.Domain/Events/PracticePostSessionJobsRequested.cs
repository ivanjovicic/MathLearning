namespace MathLearning.Domain.Events;

/// <summary>
/// Durable post-session work for a completed practice session.
/// Outbox row identity is the session id so completion enqueues at most once.
/// </summary>
public sealed record PracticePostSessionJobsRequested(
    string UserId,
    Guid SessionId) : DomainEventBase;

namespace MathLearning.Domain.Events;

public sealed record StreakProtectedByFreeze(
    int UserId,
    DateOnly MissedDate,
    int FreezeRemaining
) : DomainEventBase;

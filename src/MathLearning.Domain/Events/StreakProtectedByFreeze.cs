namespace MathLearning.Domain.Events;

public sealed record StreakProtectedByFreeze(
    string UserId,
    DateOnly MissedDate,
    int FreezeRemaining
) : DomainEventBase;

namespace MathLearning.Domain.Events;

public sealed record CoinsGranted(
    string UserId,
    int Amount,
    string Reason
) : DomainEventBase;

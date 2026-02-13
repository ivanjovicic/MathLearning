namespace MathLearning.Domain.Events;

public sealed record CoinsGranted(
    int UserId,
    int Amount,
    string Reason
) : DomainEventBase;

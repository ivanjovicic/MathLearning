namespace MathLearning.Domain.Events;

public sealed record PowerUpPurchased(
    int UserId,
    string PowerUpType,
    int Quantity,
    int CoinsSpent
) : DomainEventBase;

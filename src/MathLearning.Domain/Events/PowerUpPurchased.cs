namespace MathLearning.Domain.Events;

public sealed record PowerUpPurchased(
    string UserId,
    string PowerUpType,
    int Quantity,
    int CoinsSpent
) : DomainEventBase;

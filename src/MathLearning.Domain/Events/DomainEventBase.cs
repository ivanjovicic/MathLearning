namespace MathLearning.Domain.Events;

public abstract record DomainEventBase : IDomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredUtc { get; init; } = DateTime.UtcNow;
}

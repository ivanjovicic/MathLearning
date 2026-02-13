namespace MathLearning.Domain.Events;

public interface IDomainEvent
{
    Guid Id { get; }
    DateTime OccurredUtc { get; }
}

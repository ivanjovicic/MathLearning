using MathLearning.Domain.Events;

namespace MathLearning.Domain.Primitives;

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    protected void Raise(IDomainEvent ev) => _domainEvents.Add(ev);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

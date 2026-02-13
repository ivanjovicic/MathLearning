using MathLearning.Domain.Events;

namespace MathLearning.Infrastructure.Services.EventBus;

public interface IEventHandler<TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent ev, CancellationToken ct);
}

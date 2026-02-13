using MathLearning.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace MathLearning.Infrastructure.Services.EventBus;

public class InProcEventBus : IEventBus
{
    private readonly IServiceProvider _sp;

    public InProcEventBus(IServiceProvider sp) => _sp = sp;

    public async Task PublishAsync(string type, string payloadJson, CancellationToken ct)
    {
        var evType = Type.GetType(type, throwOnError: true)!;
        var evObj = (IDomainEvent)JsonSerializer.Deserialize(payloadJson, evType)!;

        // Resolve IEnumerable<IEventHandler<TEvent>> i pozovi
        var handlerType = typeof(IEventHandler<>).MakeGenericType(evType);
        var handlers = _sp.GetServices(handlerType);

        foreach (var h in handlers)
        {
            var method = handlerType.GetMethod("Handle")!;
            var task = (Task)method.Invoke(h, new object[] { evObj, ct })!;
            await task;
        }
    }
}

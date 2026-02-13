namespace MathLearning.Infrastructure.Services.EventBus;

public interface IEventBus
{
    Task PublishAsync(string type, string payloadJson, CancellationToken ct);
}

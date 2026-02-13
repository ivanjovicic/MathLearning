using MathLearning.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.EventBus.Handlers;

public class CoinsGrantedHandler : IEventHandler<CoinsGranted>
{
    private readonly ILogger<CoinsGrantedHandler> _logger;

    public CoinsGrantedHandler(ILogger<CoinsGrantedHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(CoinsGranted ev, CancellationToken ct)
    {
        _logger.LogInformation("🪙 Coins granted: User {UserId} received {Amount} coins for {Reason}", 
            ev.UserId, ev.Amount, ev.Reason);

        // ovde možeš dodati:
        // - analytics tracking
        // - notification
        // - achievement check (npr. "earned 100 coins total")

        return Task.CompletedTask;
    }
}

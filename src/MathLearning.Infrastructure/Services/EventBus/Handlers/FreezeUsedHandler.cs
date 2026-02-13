using MathLearning.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.EventBus.Handlers;

public class FreezeUsedHandler : IEventHandler<StreakProtectedByFreeze>
{
    private readonly ILogger<FreezeUsedHandler> _logger;

    public FreezeUsedHandler(ILogger<FreezeUsedHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(StreakProtectedByFreeze ev, CancellationToken ct)
    {
        _logger.LogInformation("❄️ Streak freeze used by user {UserId} on {MissedDate}, {Remaining} freezes remaining", 
            ev.UserId, ev.MissedDate, ev.FreezeRemaining);

        // ovde možeš dodati:
        // - analytics tracking
        // - push notification
        // - unlock achievement/badge
        // - update user statistics

        return Task.CompletedTask;
    }
}

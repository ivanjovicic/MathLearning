using MathLearning.Api.Services;
using MathLearning.Domain.Events;
using MathLearning.Infrastructure.Services.EventBus;
using Microsoft.Extensions.Logging;

namespace MathLearning.Api.Services.EventHandlers;

public sealed class PracticePostSessionJobsRequestedHandler : IEventHandler<PracticePostSessionJobsRequested>
{
    private readonly IPracticeBackgroundJobs _backgroundJobs;
    private readonly ILogger<PracticePostSessionJobsRequestedHandler> _logger;

    public PracticePostSessionJobsRequestedHandler(
        IPracticeBackgroundJobs backgroundJobs,
        ILogger<PracticePostSessionJobsRequestedHandler> logger)
    {
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    public async Task Handle(PracticePostSessionJobsRequested ev, CancellationToken ct)
    {
        await _backgroundJobs.EnqueuePostSessionJobsAsync(ev.UserId, ct);

        _logger.LogInformation(
            "Practice post-session jobs published from outbox. UserId={UserId} SessionId={SessionId}",
            ev.UserId,
            ev.SessionId);
    }
}

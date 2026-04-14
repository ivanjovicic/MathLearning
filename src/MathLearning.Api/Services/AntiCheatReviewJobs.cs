using Hangfire;
using MathLearning.Application.Services;

namespace MathLearning.Api.Services;

public interface IAntiCheatHangfireJobs
{
    Task RunMlReviewSweepJob(int take = 0);
    Task RunMlReviewForDetectionJob(Guid id);
}

public sealed class AntiCheatHangfireJobs : IAntiCheatHangfireJobs
{
    private readonly IAntiCheatMlReviewService antiCheatMlReviewService;
    private readonly ILogger<AntiCheatHangfireJobs> logger;

    public AntiCheatHangfireJobs(
        IAntiCheatMlReviewService antiCheatMlReviewService,
        ILogger<AntiCheatHangfireJobs> logger)
    {
        this.antiCheatMlReviewService = antiCheatMlReviewService;
        this.logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task RunMlReviewSweepJob(int take = 0)
    {
        var processed = await antiCheatMlReviewService.ProcessPendingReviewsAsync(take);
        logger.LogInformation(
            "Anti-cheat ML review sweep completed. Processed={Processed} RequestedTake={RequestedTake}",
            processed,
            take);
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task RunMlReviewForDetectionJob(Guid id)
    {
        var result = await antiCheatMlReviewService.ProcessReviewAsync(id);
        logger.LogInformation(
            "Anti-cheat ML review completed for DetectionId={DetectionId} Status={MlReviewStatus} Attempts={Attempts}",
            result.Id,
            result.MlReviewStatus,
            result.MlReviewAttempts);
    }
}

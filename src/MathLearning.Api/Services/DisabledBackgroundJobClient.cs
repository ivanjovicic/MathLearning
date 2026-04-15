using Hangfire;
using Hangfire.Common;
using Hangfire.States;

namespace MathLearning.Api.Services;

public sealed class DisabledBackgroundJobClient : IBackgroundJobClient
{
    private readonly ILogger<DisabledBackgroundJobClient> _logger;

    public DisabledBackgroundJobClient(ILogger<DisabledBackgroundJobClient> logger)
    {
        _logger = logger;
    }

    public string Create(Job job, IState state)
    {
        var jobId = $"disabled-{Guid.NewGuid():N}";

        _logger.LogWarning(
            "Background job creation skipped because Hangfire is disabled. JobId={JobId} Type={Type} Method={Method} State={State}",
            jobId,
            job.Type.Name,
            job.Method.Name,
            state.Name);

        return jobId;
    }

    public bool ChangeState(string jobId, IState state, string expectedState)
    {
        _logger.LogWarning(
            "Background job state change skipped because Hangfire is disabled. JobId={JobId} State={State} ExpectedState={ExpectedState}",
            jobId,
            state.Name,
            expectedState);

        return false;
    }
}
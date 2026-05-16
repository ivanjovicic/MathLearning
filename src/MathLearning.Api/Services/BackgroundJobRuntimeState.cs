namespace MathLearning.Api.Services;

public sealed class BackgroundJobRuntimeState
{
    public bool HangfireEnabled { get; set; }

    public string? DisabledReason { get; set; }
}

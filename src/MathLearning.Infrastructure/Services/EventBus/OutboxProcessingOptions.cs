namespace MathLearning.Infrastructure.Services.EventBus;

public sealed class OutboxProcessingOptions
{
    public int BatchSize { get; init; } = 50;

    public int MaxAttempts { get; init; } = 5;

    public int MaxPersistedErrorLength { get; init; } = 256;

    public TimeSpan IdleDelay { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan ErrorDelay { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan GetRetryDelay(int attemptNumber)
    {
        var exponent = Math.Clamp(attemptNumber - 1, 0, 10);
        var delay = TimeSpan.FromTicks(InitialRetryDelay.Ticks * (1L << exponent));
        return delay <= MaxRetryDelay ? delay : MaxRetryDelay;
    }
}


using Microsoft.Extensions.Configuration;

namespace MathLearning.Infrastructure.Services.EventBus;

public sealed class BackgroundWorkOptions
{
    public const string SectionName = "BackgroundWork";

    public string Profile { get; set; } = "Full";
    public bool? EnableHangfire { get; set; }
    public bool? EnableIndexMaintenance { get; set; }
    public bool? EnableXpReset { get; set; }
    public bool? EnableWeaknessDailySweep { get; set; }
    public bool? EnableExplanationCacheCleanup { get; set; }
    public int? OutboxInitialIdleDelaySeconds { get; set; }
    public int? OutboxMaxIdleDelaySeconds { get; set; }

    public bool IsPreProductionIdle =>
        string.Equals(Profile, "PreProductionIdle", StringComparison.OrdinalIgnoreCase);

    public bool HangfireEnabled => EnableHangfire ?? !IsPreProductionIdle;
    public bool IndexMaintenanceEnabled => EnableIndexMaintenance ?? !IsPreProductionIdle;
    public bool XpResetEnabled => EnableXpReset ?? !IsPreProductionIdle;
    public bool WeaknessDailySweepEnabled => EnableWeaknessDailySweep ?? !IsPreProductionIdle;
    public bool ExplanationCacheCleanupEnabled => EnableExplanationCacheCleanup ?? !IsPreProductionIdle;

    public TimeSpan OutboxInitialIdleDelay =>
        TimeSpan.FromSeconds(Math.Clamp(
            OutboxInitialIdleDelaySeconds ?? (IsPreProductionIdle ? 60 : 1),
            1,
            3600));

    public TimeSpan OutboxMaxIdleDelay =>
        TimeSpan.FromSeconds(Math.Clamp(
            OutboxMaxIdleDelaySeconds ?? (IsPreProductionIdle ? 600 : 60),
            OutboxInitialIdleDelay.TotalSeconds,
            3600));

    public static BackgroundWorkOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new BackgroundWorkOptions();
        configuration.GetSection(SectionName).Bind(options);
        return options;
    }
}

public sealed class OutboxProcessingOptions
{
    public int BatchSize { get; init; } = 50;

    public int MaxAttempts { get; init; } = 5;

    public int MaxPersistedErrorLength { get; init; } = 256;

    public TimeSpan IdleDelay { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaxIdleDelay { get; init; } = TimeSpan.FromMinutes(1);

    public TimeSpan ErrorDelay { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan GetRetryDelay(int attemptNumber)
    {
        var exponent = Math.Clamp(attemptNumber - 1, 0, 10);
        var delay = TimeSpan.FromTicks(InitialRetryDelay.Ticks * (1L << exponent));
        return delay <= MaxRetryDelay ? delay : MaxRetryDelay;
    }

    public TimeSpan GetIdleDelay(int consecutiveEmptyPolls)
    {
        var exponent = Math.Clamp(consecutiveEmptyPolls, 0, 10);
        var delay = TimeSpan.FromTicks(IdleDelay.Ticks * (1L << exponent));
        return delay <= MaxIdleDelay ? delay : MaxIdleDelay;
    }
}


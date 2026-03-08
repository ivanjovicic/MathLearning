namespace MathLearning.Infrastructure.Services.Sync;

public sealed class SyncOptions
{
    public bool RequireOperationSignatures { get; set; } = true;
    public int MaxBatchSize { get; set; } = 100;
    public int MaxServerEventsPerSync { get; set; } = 500;
    public int MaxProcessingRetries { get; set; } = 5;
    public int DefaultQuestionBundleSize { get; set; } = 100;
    public bool EnableDeadLetterRedriveWorker { get; set; } = true;
    public int DeadLetterRedriveIntervalSeconds { get; set; } = 60;
    public int DeadLetterRedriveBatchSize { get; set; } = 20;
    public int MaxDeadLetterRedriveAttempts { get; set; } = 10;
}

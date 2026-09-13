namespace MathLearning.Infrastructure.Services.Sync;

public sealed class SyncOptions
{
    public bool RequireOperationSignatures { get; set; } = true;
    public int MaxRequestBodyBytes { get; set; } = 256 * 1024;
    public int MaxOperationsPerBatch { get; set; } = 100;
    public int MaxBatchSize { get; set; } = 100;
    public int MaxOperationPayloadBytes { get; set; } = 32 * 1024;
    public int MaxTotalPayloadBytes { get; set; } = 192 * 1024;
    public int MaxDeviceIdLength { get; set; } = 128;
    public int MaxDeviceNameLength { get; set; } = 128;
    public int MaxPlatformLength { get; set; } = 32;
    public int MaxAppVersionLength { get; set; } = 64;
    public int MaxOperationTypeLength { get; set; } = 64;
    public int MaxSignatureBytes { get; set; } = 256;
    public int MaxPublicErrorMessageLength { get; set; } = 256;
    public int MaxInternalDiagnosticLength { get; set; } = 2048;
    public int MaxServerEventsPerSync { get; set; } = 500;
    public int MaxProcessingRetries { get; set; } = 5;
    public int DefaultQuestionBundleSize { get; set; } = 100;
    public bool EnableDeadLetterRedriveWorker { get; set; } = true;
    public bool EnableRetentionCleanupWorker { get; set; } = true;
    public int DeadLetterRedriveIntervalSeconds { get; set; } = 60;
    public int DeadLetterRedriveBatchSize { get; set; } = 20;
    public int MaxDeadLetterRedriveAttempts { get; set; } = 10;
    public int ProgressSyncMaxOfflineWindowDays { get; set; } = 30;
    public int RetentionCleanupIntervalSeconds { get; set; } = 86400;
    public int RetentionBatchSize { get; set; } = 500;
    public int SyncEventLogRetentionDays { get; set; } = 30;
    public int ServerSyncEventRetentionDays { get; set; } = 90;
    public int SyncDeadLetterRetentionDays { get; set; } = 30;
}

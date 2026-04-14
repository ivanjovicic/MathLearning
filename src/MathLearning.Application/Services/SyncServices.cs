using MathLearning.Application.DTOs.Sync;

namespace MathLearning.Application.Services;

public interface ISyncService
{
    Task<RegisterSyncDeviceResponse> RegisterDeviceAsync(
        string userId,
        RegisterSyncDeviceRequest request,
        CancellationToken cancellationToken);

    Task<SyncResponseDto> SyncAsync(
        string authenticatedUserId,
        SyncRequestDto request,
        CancellationToken cancellationToken);
}

public interface IOfflineBundleService
{
    Task<OfflineBundleResponseDto> GetBundleAsync(
        string userId,
        int? subtopicId,
        int questionCount,
        CancellationToken cancellationToken);
}

public interface ISyncAdminService
{
    Task<SyncAdminOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SyncDeadLetterItemDto>> GetDeadLettersAsync(
        int take,
        string? status,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SyncDeviceAdminDto>> GetDevicesAsync(
        int take,
        CancellationToken cancellationToken);

    Task<SyncDeadLetterRedriveResponseDto> RedriveDeadLetterAsync(
        Guid operationId,
        string? actorUserId,
        CancellationToken cancellationToken);

    Task<SyncDeadLetterRedriveBatchResponseDto> RedriveDeadLettersAsync(
        int? take,
        bool includeExhausted,
        string? actorUserId,
        CancellationToken cancellationToken);
}

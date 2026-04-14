using MathLearning.Application.DTOs.DesignTokens;

namespace MathLearning.Application.Services;

public interface IDesignTokenQueryService
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken);
    Task<DesignTokensResponse> GetCurrentTokensAsync(string? theme, CancellationToken cancellationToken);
    Task<DesignTokensResponse> GetCurrentTokensByThemeAsync(string theme, CancellationToken cancellationToken);
    Task<DesignTokenVersionResponse> GetCurrentVersionAsync(CancellationToken cancellationToken);
}

public interface IDesignTokenAdminService
{
    Task<AdminDesignTokensResponse> GetAdminTokensAsync(CancellationToken cancellationToken);
    Task<DesignTokensResponse> UpsertDraftAsync(
        UpsertDesignTokensRequest request,
        string? actorUserId,
        string? actorName,
        string? correlationId,
        CancellationToken cancellationToken);
    Task<DesignTokenVersionResponse> PublishDraftAsync(
        PublishDesignTokenVersionRequest request,
        string? actorUserId,
        string? actorName,
        string? correlationId,
        CancellationToken cancellationToken);
    Task<DesignTokenVersionResponse> RollbackAsync(
        string version,
        RollbackDesignTokenVersionRequest request,
        string? actorUserId,
        string? actorName,
        string? correlationId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<DesignTokenHistoryItemResponse>> GetHistoryAsync(CancellationToken cancellationToken);
}

public interface IDesignTokenCacheService
{
    string BuildKey(string version, string theme);
    Task<DesignTokensResponse?> GetAsync(string version, string theme, CancellationToken cancellationToken);
    Task SetAsync(string version, string theme, DesignTokensResponse response, CancellationToken cancellationToken);
    Task RemoveAsync(string version, string theme, CancellationToken cancellationToken);
}

public interface IDesignTokenMergeService
{
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Merge(
        string theme,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> databaseTokens);
}

public interface IDesignTokenCompilerService
{
    DesignTokensResponse Compile(
        string version,
        int baseWidth,
        string theme,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> mergedTokens);
}

public interface IDesignTokenVersionManager
{
    string EnsureNextVersion(string? requestedVersion, string? currentVersion);
    string CreateRollbackVersion(string? requestedVersion, string currentVersion);
}

public interface IDesignTokenAuditService
{
    Task WriteAsync(
        string action,
        string? actorUserId,
        string? actorName,
        string? correlationId,
        Guid? versionId,
        Guid? tokenSetId,
        string? theme,
        string? beforeSnapshotJson,
        string? afterSnapshotJson,
        object? metadata,
        CancellationToken cancellationToken);
}

public static class DesignTokenSecurity
{
    public const string AdminRole = "UiTokensAdmin";
    public const string AdminPolicy = "UiTokensAdminPolicy";
}

using MathLearning.Application.DTOs.Cosmetics;

namespace MathLearning.Application.Services;

public interface ICosmeticCatalogService
{
    Task<CosmeticCatalogResponseDto> GetCatalogAsync(
        string userId,
        string? category,
        string? rarity,
        int? seasonId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CosmeticSeasonDto>> GetSeasonsAsync(
        bool activeOnly,
        CancellationToken cancellationToken);

    Task<RewardTrackResponseDto?> GetRewardTrackAsync(
        string userId,
        int? seasonId,
        string trackType,
        CancellationToken cancellationToken);
}

public interface ICosmeticInventoryService
{
    Task<CosmeticInventoryResponseDto> GetInventoryAsync(
        string userId,
        string? category,
        CancellationToken cancellationToken);

    Task<AvatarAppearanceDto> GetAvatarAsync(string userId, CancellationToken cancellationToken);

    Task<AvatarAppearanceDto> GetPublicAppearanceAsync(string userId, CancellationToken cancellationToken);

    Task<AvatarAppearanceDto> UpdateAvatarAsync(
        string userId,
        UpdateAvatarConfigRequest request,
        CancellationToken cancellationToken);

    Task<AvatarAppearanceDto> EquipSlotAsync(
        string userId,
        EquipCosmeticRequest request,
        CancellationToken cancellationToken);

    Task<AvatarAppearanceDto> EquipBatchAsync(
        string userId,
        EquipCosmeticBatchRequest request,
        CancellationToken cancellationToken);

    Task<PurchaseCosmeticResponse> PurchaseAsync(
        string userId,
        PurchaseCosmeticRequest request,
        CancellationToken cancellationToken);
}

public interface ICosmeticRewardService
{
    Task<IReadOnlyList<CosmeticUnlockResultDto>> ProcessProgressRewardsAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CosmeticUnlockResultDto>> ProcessRewardSourceAsync(
        CosmeticRewardSourceRequest request,
        CancellationToken cancellationToken);
}

public interface ICosmeticAdminService
{
    Task<IReadOnlyList<AdminCosmeticItemDto>> GetItemsAsync(CancellationToken cancellationToken);
    Task<AdminCosmeticItemDto> UpsertItemAsync(int? id, UpsertCosmeticItemRequest request, string? actorUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CosmeticRewardRuleDto>> GetRewardRulesAsync(CancellationToken cancellationToken);
    Task<CosmeticRewardRuleDto> UpsertRewardRuleAsync(int? id, UpsertCosmeticRewardRuleRequest request, string? actorUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CosmeticSeasonDto>> GetAdminSeasonsAsync(CancellationToken cancellationToken);
    Task<CosmeticSeasonDto> UpsertSeasonAsync(int? id, UpsertCosmeticSeasonRequest request, string? actorUserId, CancellationToken cancellationToken);
    Task<RewardTrackTierDto> UpsertRewardTrackAsync(int? id, UpsertRewardTrackEntryRequest request, string? actorUserId, CancellationToken cancellationToken);
    Task<CosmeticAnalyticsSummaryDto> GetAnalyticsSummaryAsync(CancellationToken cancellationToken);
}

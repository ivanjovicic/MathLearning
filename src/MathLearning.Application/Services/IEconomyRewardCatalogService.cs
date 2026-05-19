using MathLearning.Domain.Entities;

namespace MathLearning.Application.Services;

public interface IEconomyRewardCatalogService
{
    Task<ResolvedEconomyReward?> ResolveAsync(
        string rewardId,
        string? rewardType,
        UserProfile profile,
        CancellationToken cancellationToken = default);
}

public sealed record ResolvedEconomyReward(
    string RewardId,
    string RewardType,
    int Coins,
    int Xp,
    bool IsEligible,
    bool IsSingleUse,
    string IneligibilityMessage
);
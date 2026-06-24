namespace MathLearning.Application.Services;

public interface ICosmeticsFragmentService
{
    Task<CosmeticFragmentTarget?> ResolveFragmentTargetAsync(string fragmentName, CancellationToken cancellationToken);

    Task<FragmentGrantResult> GrantFragmentsAsync(
        string userId,
        string fragmentName,
        int copies,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}

public sealed record CosmeticFragmentTarget(
    int CosmeticItemId,
    string ItemKey,
    string FragmentLabel,
    int RequiredFragments);

public sealed record FragmentGrantResult(
    bool ItemUnlocked,
    string? UnlockedItemKey,
    DateTime? UnlockedAt,
    string? UnlockedSource,
    int Collected,
    int Required,
    DateTime UpdatedAtUtc,
    DateTime? ProgressUnlockedAtUtc);

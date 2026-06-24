using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services.Cosmetics;

public sealed class CosmeticsFragmentService : ICosmeticsFragmentService
{
    public const int DefaultRequiredFragments = 5;

    private static readonly IReadOnlyDictionary<string, string> LegacyFragmentLabelToItemKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Comet Frame Fragment"] = "frame_comet",
            ["Nova Trail Fragment"] = "effect_nova_trail",
            ["Neon Number Burst Fragment"] = "effect_neon_number_burst",
            ["Solar Pulse Fragment"] = "solar-pulse"
        };

    private readonly ApiDbContext db;

    public CosmeticsFragmentService(ApiDbContext db)
    {
        this.db = db;
    }

    public async Task<CosmeticFragmentTarget?> ResolveFragmentTargetAsync(
        string fragmentName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fragmentName))
            return null;

        var normalized = fragmentName.Trim();
        var byLabel = await db.CosmeticItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsActive && x.FragmentLabel == normalized, cancellationToken);
        if (byLabel is not null)
        {
            return ToTarget(byLabel, normalized);
        }

        if (!LegacyFragmentLabelToItemKey.TryGetValue(normalized, out var itemKey))
            return null;

        var byKey = await db.CosmeticItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsActive && x.Key == itemKey, cancellationToken);

        return byKey is null ? null : ToTarget(byKey, normalized);
    }

    public async Task<FragmentGrantResult> GrantFragmentsAsync(
        string userId,
        string fragmentName,
        int copies,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var target = await ResolveFragmentTargetAsync(fragmentName, cancellationToken)
            ?? throw new InvalidOperationException($"Fragment '{fragmentName}' is not recognized.");

        var alreadyOwned = await db.UserCosmeticInventories
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.CosmeticItemId == target.CosmeticItemId && !x.IsRevoked, cancellationToken);

        var progress = await db.UserCosmeticFragmentProgresses
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CosmeticItemId == target.CosmeticItemId, cancellationToken);

        if (progress is null)
        {
            progress = new UserCosmeticFragmentProgress
            {
                UserId = userId,
                CosmeticItemId = target.CosmeticItemId,
                Collected = 0,
                Required = target.RequiredFragments,
                UpdatedAtUtc = nowUtc
            };
            db.UserCosmeticFragmentProgresses.Add(progress);
        }

        var itemUnlocked = false;
        string? unlockedSource = null;
        DateTime? inventoryUnlockedAt = null;
        var isUnlocked = alreadyOwned || progress.UnlockedAtUtc.HasValue;

        if (!isUnlocked)
        {
            var newCollected = Math.Min(progress.Collected + copies, progress.Required);
            progress.Collected = newCollected;
            progress.UpdatedAtUtc = nowUtc;

            if (newCollected >= progress.Required)
            {
                progress.Collected = progress.Required;
                progress.UnlockedAtUtc = nowUtc;

                var existingInventory = await db.UserCosmeticInventories
                    .FirstOrDefaultAsync(
                        x => x.UserId == userId && x.CosmeticItemId == target.CosmeticItemId && !x.IsRevoked,
                        cancellationToken);

                if (existingInventory is null)
                {
                    existingInventory = new UserCosmeticInventory
                    {
                        UserId = userId,
                        CosmeticItemId = target.CosmeticItemId,
                        Source = "fragment_unlock",
                        SourceRef = $"fragment:{target.FragmentLabel}",
                        GrantReason = $"Unlocked via {target.FragmentLabel}",
                        AssetVersion = "1",
                        UnlockedAt = nowUtc
                    };
                    db.UserCosmeticInventories.Add(existingInventory);
                    itemUnlocked = true;
                }

                unlockedSource = existingInventory.Source;
                inventoryUnlockedAt = existingInventory.UnlockedAt;
            }
        }
        else
        {
            progress.Collected = Math.Min(progress.Collected + copies, progress.Required);
            progress.UpdatedAtUtc = nowUtc;
            if (!progress.UnlockedAtUtc.HasValue)
            {
                inventoryUnlockedAt = await db.UserCosmeticInventories
                    .AsNoTracking()
                    .Where(x => x.UserId == userId && x.CosmeticItemId == target.CosmeticItemId && !x.IsRevoked)
                    .Select(x => (DateTime?)x.UnlockedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                progress.UnlockedAtUtc = inventoryUnlockedAt ?? nowUtc;
            }
        }

        return new FragmentGrantResult(
            ItemUnlocked: itemUnlocked,
            UnlockedItemKey: target.ItemKey,
            UnlockedAt: inventoryUnlockedAt ?? progress.UnlockedAtUtc,
            UnlockedSource: unlockedSource,
            Collected: progress.Collected,
            Required: progress.Required,
            UpdatedAtUtc: progress.UpdatedAtUtc,
            ProgressUnlockedAtUtc: progress.UnlockedAtUtc);
    }

    public static async Task<IReadOnlyDictionary<string, int>> LoadFragmentProgressByLabelAsync(
        ApiDbContext db,
        string userId,
        CancellationToken cancellationToken)
    {
        var rows = await db.UserCosmeticFragmentProgresses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Join(
                db.CosmeticItems.AsNoTracking(),
                progress => progress.CosmeticItemId,
                item => item.Id,
                (progress, item) => new { progress, item })
            .OrderBy(x => x.item.FragmentLabel ?? x.item.Key)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var label = row.item.FragmentLabel;
            if (string.IsNullOrWhiteSpace(label))
            {
                foreach (var (legacyLabel, itemKey) in LegacyFragmentLabelToItemKey)
                {
                    if (string.Equals(itemKey, row.item.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        label = legacyLabel;
                        break;
                    }
                }
            }

            label ??= row.item.Key;
            result[label] = row.progress.Collected;
        }

        return result;
    }

    private static CosmeticFragmentTarget ToTarget(CosmeticItem item, string fragmentLabel)
    {
        return new CosmeticFragmentTarget(
            item.Id,
            item.Key,
            fragmentLabel,
            item.FragmentsRequired ?? DefaultRequiredFragments);
    }
}

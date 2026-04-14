using System.Text.Json;
using FluentValidation;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Domain.Entities;

namespace MathLearning.Application.Validators;

public sealed class UpsertCosmeticItemRequestValidator : AbstractValidator<UpsertCosmeticItemRequest>
{
    public UpsertCosmeticItemRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Category)
            .NotEmpty()
            .Must(BeKnownCategory)
            .WithMessage("Unsupported cosmetic category.");
        RuleFor(x => x.Rarity).NotEmpty().MaximumLength(32);
        RuleFor(x => x.AssetPath).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PreviewAssetPath).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.PreviewAssetPath));
        RuleFor(x => x.UnlockType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.AssetVersion).NotEmpty().MaximumLength(32);
        RuleFor(x => x.CoinPrice).GreaterThanOrEqualTo(0).When(x => x.CoinPrice.HasValue);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnlockConditionJson)
            .Must(BeValidJson)
            .WithMessage("UnlockConditionJson must be valid JSON.")
            .When(x => !string.IsNullOrWhiteSpace(x.UnlockConditionJson));
        RuleFor(x => x.CompatibilityRulesJson)
            .Must(BeValidJson)
            .WithMessage("CompatibilityRulesJson must be valid JSON.")
            .When(x => !string.IsNullOrWhiteSpace(x.CompatibilityRulesJson));
        RuleFor(x => x)
            .Must(x => !x.ReleaseDate.HasValue || !x.RetirementDate.HasValue || x.ReleaseDate <= x.RetirementDate)
            .WithMessage("ReleaseDate must be before RetirementDate.");
    }

    private static bool BeKnownCategory(string category)
    {
        var normalized = category.Trim().ToLowerInvariant();
        return normalized is
            CosmeticCategories.Skin or
            CosmeticCategories.Hair or
            CosmeticCategories.Clothing or
            CosmeticCategories.Accessory or
            CosmeticCategories.Emoji or
            CosmeticCategories.Frame or
            CosmeticCategories.Background or
            CosmeticCategories.Effect or
            CosmeticCategories.LeaderboardDecoration;
    }

    private static bool BeValidJson(string? value)
    {
        try
        {
            _ = JsonDocument.Parse(value!);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class UpsertCosmeticRewardRuleRequestValidator : AbstractValidator<UpsertCosmeticRewardRuleRequest>
{
    public UpsertCosmeticRewardRuleRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.SourceType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.RewardType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RewardPayloadJson).NotEmpty().Must(BeValidJson).WithMessage("RewardPayloadJson must be valid JSON.");
        RuleFor(x => x.ConditionJson)
            .Must(BeValidJson)
            .WithMessage("ConditionJson must be valid JSON.")
            .When(x => !string.IsNullOrWhiteSpace(x.ConditionJson));
    }

    private static bool BeValidJson(string? value)
    {
        try
        {
            _ = JsonDocument.Parse(value!);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class UpsertCosmeticSeasonRequestValidator : AbstractValidator<UpsertCosmeticSeasonRequest>
{
    public UpsertCosmeticSeasonRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Theme).MaximumLength(128).When(x => !string.IsNullOrWhiteSpace(x.Theme));
        RuleFor(x => x.ThemeAssetPath).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.ThemeAssetPath));
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(x => x.Trim().ToLowerInvariant() is
                CosmeticSeasonStatuses.Draft or
                CosmeticSeasonStatuses.Scheduled or
                CosmeticSeasonStatuses.Active or
                CosmeticSeasonStatuses.RewardLock or
                CosmeticSeasonStatuses.Completed or
                CosmeticSeasonStatuses.Archived)
            .WithMessage("Unsupported season status.");
        RuleFor(x => x)
            .Must(x => x.EndDate > x.StartDate)
            .WithMessage("EndDate must be after StartDate.");
        RuleFor(x => x)
            .Must(x => !x.RewardLockAt.HasValue || x.RewardLockAt >= x.StartDate)
            .WithMessage("RewardLockAt must be after StartDate.");
        RuleFor(x => x)
            .Must(x => !x.ArchiveAt.HasValue || x.ArchiveAt >= x.EndDate)
            .WithMessage("ArchiveAt must be after EndDate.");
    }
}

public sealed class UpsertRewardTrackEntryRequestValidator : AbstractValidator<UpsertRewardTrackEntryRequest>
{
    public UpsertRewardTrackEntryRequestValidator()
    {
        RuleFor(x => x.SeasonId).GreaterThan(0);
        RuleFor(x => x.TrackType)
            .NotEmpty()
            .Must(x => x.Trim().ToLowerInvariant() is CosmeticTrackTypes.Free or CosmeticTrackTypes.Premium)
            .WithMessage("Unsupported track type.");
        RuleFor(x => x.Tier).GreaterThan(0);
        RuleFor(x => x.XpRequired).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RewardType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.RewardPayloadJson).NotEmpty().Must(BeValidJson).WithMessage("RewardPayloadJson must be valid JSON.");
    }

    private static bool BeValidJson(string? value)
    {
        try
        {
            _ = JsonDocument.Parse(value!);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class ClaimRewardTrackTierRequestValidator : AbstractValidator<ClaimRewardTrackTierRequest>
{
    public ClaimRewardTrackTierRequestValidator()
    {
        RuleFor(x => x.TrackType)
            .NotEmpty()
            .Must(x => x.Trim().ToLowerInvariant() is CosmeticTrackTypes.Free or CosmeticTrackTypes.Premium)
            .WithMessage("Unsupported track type.");
        RuleFor(x => x.Tier).GreaterThan(0);
        RuleFor(x => x.SeasonId).GreaterThan(0).When(x => x.SeasonId.HasValue);
    }
}

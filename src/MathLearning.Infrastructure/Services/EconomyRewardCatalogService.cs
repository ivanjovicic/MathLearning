using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services;

public sealed class EconomyRewardCatalogService : IEconomyRewardCatalogService
{
    private readonly ApiDbContext db;

    public EconomyRewardCatalogService(ApiDbContext db)
    {
        this.db = db;
    }

    public async Task<ResolvedEconomyReward?> ResolveAsync(
        string rewardId,
        string? rewardType,
        UserProfile profile,
        CancellationToken cancellationToken = default)
    {
        var normalizedRewardId = NormalizeRequired(rewardId, nameof(rewardId));
        var requestedRewardType = Normalize(rewardType);
        var inferredRewardType = InferRewardType(normalizedRewardId);

        if (!string.IsNullOrWhiteSpace(requestedRewardType) &&
            !string.IsNullOrWhiteSpace(inferredRewardType) &&
            !string.Equals(requestedRewardType, inferredRewardType, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var effectiveRewardType = string.IsNullOrWhiteSpace(requestedRewardType)
            ? inferredRewardType
            : requestedRewardType;

        if (string.IsNullOrWhiteSpace(effectiveRewardType))
            return null;

        var definitions = await db.EconomyRewardDefinitions
            .AsNoTracking()
            .Where(x => x.IsActive && x.RewardType == effectiveRewardType)
            .OrderBy(x => x.Priority)
            .ThenByDescending(x => x.RewardIdPattern.Length)
            .ToListAsync(cancellationToken);

        foreach (var definition in definitions)
        {
            var resolved = TryResolve(definition, normalizedRewardId, effectiveRewardType, profile);
            if (resolved is not null)
                return resolved;
        }

        return null;
    }

    private static ResolvedEconomyReward? TryResolve(
        EconomyRewardDefinition definition,
        string rewardId,
        string rewardType,
        UserProfile profile)
    {
        Match match;
        try
        {
            match = new Regex(
                    definition.RewardIdPattern,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Match(rewardId);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (!match.Success)
            return null;

        var context = new RewardRuleContext(profile, match);

        if (!TryParseJsonObject(definition.GrantRuleJson, out var grantRule))
            return null;

        if (!TryEvaluateInt(grantRule["coins"], context, out var coins) ||
            !TryEvaluateInt(grantRule["xp"], context, out var xp) ||
            coins < 0 ||
            xp < 0)
        {
            return null;
        }

        var isEligible = true;
        if (!string.IsNullOrWhiteSpace(definition.EligibilityRuleJson))
        {
            if (!TryParseJsonNode(definition.EligibilityRuleJson, out var eligibilityRule) ||
                !TryEvaluateCondition(eligibilityRule, context, out isEligible))
            {
                return null;
            }
        }

        return new ResolvedEconomyReward(
            rewardId,
            rewardType,
            coins,
            xp,
            isEligible,
            definition.IsSingleUse,
            string.IsNullOrWhiteSpace(definition.IneligibilityMessage)
                ? "Reward is not eligible."
                : definition.IneligibilityMessage);
    }

    private static bool TryParseJsonObject(string json, out JsonObject obj)
    {
        obj = null!;
        if (!TryParseJsonNode(json, out var node) || node is not JsonObject jsonObject)
            return false;

        obj = jsonObject;
        return true;
    }

    private static bool TryParseJsonNode(string? json, out JsonNode node)
    {
        node = null!;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var parsed = JsonNode.Parse(json);
            if (parsed is null)
                return false;

            node = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryEvaluateCondition(JsonNode? node, RewardRuleContext context, out bool value)
    {
        value = false;
        if (node is not JsonObject obj || !TryReadString(obj, "type", out var type))
            return false;

        switch (type)
        {
            case "always":
                value = true;
                return true;

            case "compare":
                if (!TryEvaluateInt(obj["left"], context, out var left) ||
                    !TryEvaluateInt(obj["right"], context, out var right) ||
                    !TryReadString(obj, "operator", out var op))
                {
                    return false;
                }

                switch (op)
                {
                    case "gt":
                        value = left > right;
                        return true;
                    case "gte":
                        value = left >= right;
                        return true;
                    case "lt":
                        value = left < right;
                        return true;
                    case "lte":
                        value = left <= right;
                        return true;
                    case "eq":
                        value = left == right;
                        return true;
                    default:
                        return false;
                }

            case "all":
                if (obj["conditions"] is not JsonArray allConditions)
                    return false;

                value = true;
                foreach (var condition in allConditions)
                {
                    if (!TryEvaluateCondition(condition, context, out var itemValue))
                        return false;

                    value &= itemValue;
                    if (!value)
                        break;
                }

                return true;

            case "any":
                if (obj["conditions"] is not JsonArray anyConditions)
                    return false;

                value = false;
                foreach (var condition in anyConditions)
                {
                    if (!TryEvaluateCondition(condition, context, out var itemValue))
                        return false;

                    value |= itemValue;
                    if (value)
                        break;
                }

                return true;

            default:
                return false;
        }
    }

    private static bool TryEvaluateInt(JsonNode? node, RewardRuleContext context, out int value)
    {
        value = 0;
        if (node is null)
            return false;

        if (node is JsonValue literalValue && literalValue.TryGetValue<int>(out value))
            return true;

        if (node is not JsonObject obj || !TryReadString(obj, "type", out var type))
            return false;

        switch (type)
        {
            case "const":
                return TryReadInt(obj, "value", out value);

            case "profile":
                return TryReadProfileField(context.Profile, obj, out value);

            case "capture":
                return TryReadCapture(context.Match, obj, out value);

            case "multiply":
                if (!TryEvaluateInt(obj["left"], context, out var left) ||
                    !TryEvaluateInt(obj["right"], context, out var right))
                {
                    return false;
                }

                value = left * right;
                return true;

            case "add":
                if (obj["terms"] is not JsonArray terms)
                    return false;

                var sum = 0;
                foreach (var term in terms)
                {
                    if (!TryEvaluateInt(term, context, out var termValue))
                        return false;

                    sum += termValue;
                }

                value = sum;
                return true;

            case "clamp":
                if (!TryEvaluateInt(obj["value"], context, out var rawValue))
                    return false;

                if (obj["min"] is not null)
                {
                    if (!TryEvaluateInt(obj["min"], context, out var minValue))
                        return false;

                    rawValue = Math.Max(minValue, rawValue);
                }

                if (obj["max"] is not null)
                {
                    if (!TryEvaluateInt(obj["max"], context, out var maxValue))
                        return false;

                    rawValue = Math.Min(maxValue, rawValue);
                }

                value = rawValue;
                return true;

            default:
                return false;
        }
    }

    private static bool TryReadProfileField(UserProfile profile, JsonObject obj, out int value)
    {
        value = 0;
        if (!TryReadString(obj, "field", out var field))
            return false;

        switch (field)
        {
            case "coins":
                value = profile.Coins;
                return true;
            case "level":
                value = profile.Level;
                return true;
            case "streak":
                value = profile.Streak;
                return true;
            case "xp":
                value = profile.Xp;
                return true;
            case "totalCoinsEarned":
                value = profile.TotalCoinsEarned;
                return true;
            case "totalCoinsSpent":
                value = profile.TotalCoinsSpent;
                return true;
            default:
                return false;
        }
    }

    private static bool TryReadCapture(Match match, JsonObject obj, out int value)
    {
        value = 0;
        if (!TryReadString(obj, "name", out var name))
            return false;

        var group = match.Groups[name];
        return group.Success && int.TryParse(group.Value, out value);
    }

    private static bool TryReadString(JsonObject obj, string propertyName, out string value)
    {
        value = string.Empty;
        if (obj[propertyName] is not JsonNode node)
            return false;

        string? raw;
        try
        {
            raw = node.GetValue<string>();
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        value = raw.Trim();
        return true;
    }

    private static bool TryReadInt(JsonObject obj, string propertyName, out int value)
    {
        value = 0;
        return obj[propertyName] is JsonValue node && node.TryGetValue<int>(out value);
    }

    private static string InferRewardType(string rewardId)
    {
        var separator = rewardId.IndexOf(':');
        if (separator <= 0)
            return string.Empty;

        return Normalize(rewardId[..separator]);
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} is required.", paramName);

        return value.Trim();
    }

    private sealed record RewardRuleContext(UserProfile Profile, Match Match);
}
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathLearning.Application.DTOs.DesignTokens;
using MathLearning.Application.Services;

namespace MathLearning.Infrastructure.Services.DesignTokens;

public sealed class DesignTokenCompilerService : IDesignTokenCompilerService
{
    private static readonly Regex HexColorRegex = new("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.Compiled);

    public DesignTokensResponse Compile(
        string version,
        int baseWidth,
        string theme,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> mergedTokens)
    {
        if (baseWidth <= 0)
        {
            throw new InvalidOperationException("BaseWidth must be greater than zero.");
        }

        foreach (var required in DesignTokenCategories.Required)
        {
            if (!mergedTokens.TryGetValue(required, out var values) || values.Count == 0)
            {
                throw new InvalidOperationException($"Missing required token category '{required}'.");
            }
        }

        var additional = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in mergedTokens.Where(x => !DesignTokenCategories.Required.Contains(x.Key)))
        {
            additional[category.Key] = BuildCategoryElement(category.Value);
        }

        return new DesignTokensResponse
        {
            Version = version,
            BaseWidth = baseWidth,
            Theme = DesignTokenMergeService.NormalizeTheme(theme),
            Spacing = ParseNumericGroup(mergedTokens[DesignTokenCategories.Spacing], allowZero: true),
            Typography = ParseNumericGroup(mergedTokens[DesignTokenCategories.Typography], allowZero: false),
            Radius = ParseNumericGroup(mergedTokens[DesignTokenCategories.Radius], allowZero: true),
            Colors = ParseColorGroup(mergedTokens[DesignTokenCategories.Colors]),
            IconSizes = ParseNumericGroup(mergedTokens[DesignTokenCategories.IconSizes], allowZero: false),
            Motion = ParseNumericGroup(mergedTokens[DesignTokenCategories.MotionDurations], allowZero: false),
            AdditionalCategories = additional.Count == 0 ? null : additional
        };
    }

    private static IReadOnlyDictionary<string, decimal> ParseNumericGroup(
        IReadOnlyDictionary<string, string> tokens,
        bool allowZero)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            var value = JsonSerializer.Deserialize<decimal>(token.Value);
            if (value < 0 || (!allowZero && value <= 0))
            {
                throw new InvalidOperationException($"Invalid numeric token '{token.Key}'.");
            }

            result[token.Key] = value;
        }

        return new ReadOnlyDictionary<string, decimal>(result);
    }

    private static IReadOnlyDictionary<string, string> ParseColorGroup(IReadOnlyDictionary<string, string> tokens)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            var value = JsonSerializer.Deserialize<string>(token.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Color token '{token.Key}' cannot be empty.");
            }

            if (!value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("hsl", StringComparison.OrdinalIgnoreCase) &&
                !HexColorRegex.IsMatch(value))
            {
                throw new InvalidOperationException($"Color token '{token.Key}' is not a supported format.");
            }

            result[token.Key] = value;
        }

        return new ReadOnlyDictionary<string, string>(result);
    }

    private static JsonElement BuildCategoryElement(IReadOnlyDictionary<string, string> tokens)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            values[token.Key] = JsonDocument.Parse(token.Value).RootElement.Clone();
        }

        return JsonSerializer.SerializeToElement(values);
    }
}

using System.Collections.ObjectModel;
using System.Text.Json;
using MathLearning.Application.Services;
using Microsoft.Extensions.Options;

namespace MathLearning.Infrastructure.Services.DesignTokens;

public sealed class DesignTokenMergeService : IDesignTokenMergeService
{
    private readonly DesignTokenOptions defaults = DesignTokenDefaults.Create();
    private readonly IOptions<DesignTokenOptions> options;

    public DesignTokenMergeService(IOptions<DesignTokenOptions> options)
    {
        this.options = options;
    }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Merge(
        string theme,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> databaseTokens)
    {
        var normalizedTheme = NormalizeTheme(theme);
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        ApplyTheme(result, defaults, normalizedTheme);
        ApplyTheme(result, options.Value, normalizedTheme);

        foreach (var category in databaseTokens)
        {
            if (!result.TryGetValue(category.Key, out var values))
            {
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result[category.Key] = values;
            }

            foreach (var token in category.Value)
            {
                values[token.Key] = token.Value;
            }
        }

        return new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(
            result.ToDictionary(
                x => x.Key,
                x => (IReadOnlyDictionary<string, string>)new ReadOnlyDictionary<string, string>(x.Value),
                StringComparer.OrdinalIgnoreCase));
    }

    private static void ApplyTheme(
        IDictionary<string, Dictionary<string, string>> destination,
        DesignTokenOptions source,
        string theme)
    {
        if (!source.Themes.TryGetValue(theme, out var themeOptions))
        {
            return;
        }

        ApplyNumericCategory(destination, DesignTokenCategories.Spacing, themeOptions.Spacing);
        ApplyNumericCategory(destination, DesignTokenCategories.Typography, themeOptions.Typography);
        ApplyNumericCategory(destination, DesignTokenCategories.Radius, themeOptions.Radius);
        ApplyStringCategory(destination, DesignTokenCategories.Colors, themeOptions.Colors);
        ApplyNumericCategory(destination, DesignTokenCategories.IconSizes, themeOptions.IconSizes);
        ApplyNumericCategory(destination, DesignTokenCategories.MotionDurations, themeOptions.MotionDurations);
    }

    private static void ApplyNumericCategory(
        IDictionary<string, Dictionary<string, string>> destination,
        string category,
        IDictionary<string, decimal> values)
    {
        if (!destination.TryGetValue(category, out var target))
        {
            target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            destination[category] = target;
        }

        foreach (var value in values)
        {
            target[value.Key] = JsonSerializer.Serialize(value.Value);
        }
    }

    private static void ApplyStringCategory(
        IDictionary<string, Dictionary<string, string>> destination,
        string category,
        IDictionary<string, string> values)
    {
        if (!destination.TryGetValue(category, out var target))
        {
            target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            destination[category] = target;
        }

        foreach (var value in values)
        {
            target[value.Key] = JsonSerializer.Serialize(value.Value);
        }
    }

    internal static string NormalizeTheme(string theme) =>
        string.IsNullOrWhiteSpace(theme) ? "light" : theme.Trim().ToLowerInvariant();
}

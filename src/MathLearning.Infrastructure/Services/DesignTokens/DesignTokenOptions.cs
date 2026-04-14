using System.Text.Json;

namespace MathLearning.Infrastructure.Services.DesignTokens;

public sealed class DesignTokenOptions
{
    public int BaseWidth { get; set; } = 390;
    public Dictionary<string, DesignTokenThemeOptions> Themes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int CacheTtlMinutes { get; set; } = 60;
}

public sealed class DesignTokenThemeOptions
{
    public Dictionary<string, decimal> Spacing { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> Typography { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> Radius { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Colors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> IconSizes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> MotionDurations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal static class DesignTokenCategories
{
    public const string Spacing = "spacing";
    public const string Typography = "typography";
    public const string Radius = "radius";
    public const string Colors = "colors";
    public const string IconSizes = "iconSizes";
    public const string MotionDurations = "motionDurations";

    public static readonly HashSet<string> Required = new(StringComparer.OrdinalIgnoreCase)
    {
        Spacing,
        Typography,
        Radius,
        Colors,
        IconSizes,
        MotionDurations
    };
}

internal static class DesignTokenDefaults
{
    public static DesignTokenOptions Create()
    {
        return new DesignTokenOptions
        {
            BaseWidth = 390,
            Themes = new Dictionary<string, DesignTokenThemeOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["light"] = new()
                {
                    Spacing = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["xs"] = 4,
                        ["s"] = 8,
                        ["m"] = 16,
                        ["l"] = 24,
                        ["xl"] = 32
                    },
                    Typography = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["titleLarge"] = 24,
                        ["bodyLarge"] = 16,
                        ["caption"] = 12
                    },
                    Radius = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["small"] = 8,
                        ["medium"] = 16,
                        ["large"] = 24
                    },
                    Colors = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["background"] = "#FFF8F1",
                        ["surface"] = "#FFFFFF",
                        ["textPrimary"] = "#1F2937",
                        ["textSecondary"] = "#6B7280",
                        ["accent"] = "#F97316"
                    },
                    IconSizes = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["s"] = 16,
                        ["m"] = 20,
                        ["l"] = 24
                    },
                    MotionDurations = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["fast"] = 120,
                        ["normal"] = 220,
                        ["slow"] = 320
                    }
                },
                ["dark"] = new()
                {
                    Spacing = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["xs"] = 4,
                        ["s"] = 8,
                        ["m"] = 16,
                        ["l"] = 24,
                        ["xl"] = 32
                    },
                    Typography = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["titleLarge"] = 24,
                        ["bodyLarge"] = 16,
                        ["caption"] = 12
                    },
                    Radius = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["small"] = 8,
                        ["medium"] = 16,
                        ["large"] = 24
                    },
                    Colors = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["background"] = "#111827",
                        ["surface"] = "#1F2937",
                        ["textPrimary"] = "#F9FAFB",
                        ["textSecondary"] = "#D1D5DB",
                        ["accent"] = "#FB923C"
                    },
                    IconSizes = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["s"] = 16,
                        ["m"] = 20,
                        ["l"] = 24
                    },
                    MotionDurations = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["fast"] = 120,
                        ["normal"] = 220,
                        ["slow"] = 320
                    }
                }
            }
        };
    }
}

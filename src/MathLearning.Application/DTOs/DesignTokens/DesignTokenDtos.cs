using System.Text.Json;
using System.Text.Json.Serialization;

namespace MathLearning.Application.DTOs.DesignTokens;

public sealed record DesignTokensResponse
{
    public string Version { get; init; } = "1.0.0";
    public int BaseWidth { get; init; } = 390;
    public string Theme { get; init; } = "light";
    public IReadOnlyDictionary<string, decimal> Spacing { get; init; } = new Dictionary<string, decimal>();
    public IReadOnlyDictionary<string, decimal> Typography { get; init; } = new Dictionary<string, decimal>();
    public IReadOnlyDictionary<string, decimal> Radius { get; init; } = new Dictionary<string, decimal>();
    public IReadOnlyDictionary<string, string> Colors { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, decimal> IconSizes { get; init; } = new Dictionary<string, decimal>();

    [JsonPropertyName("motion")]
    public IReadOnlyDictionary<string, decimal> Motion { get; init; } = new Dictionary<string, decimal>();

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalCategories { get; init; }
}

public sealed record DesignTokenVersionResponse(
    string Version,
    int BaseWidth,
    DateTime? PublishedAtUtc,
    IReadOnlyList<string> Themes);

public sealed record DesignTokenHistoryItemResponse(
    string Version,
    string Status,
    int BaseWidth,
    bool IsCurrent,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc,
    string? Notes,
    IReadOnlyList<string> Themes);

public sealed record AdminDesignTokensResponse(
    DesignTokenVersionResponse? PublishedVersion,
    DesignTokenVersionResponse? DraftVersion,
    IReadOnlyDictionary<string, DesignTokensResponse> Themes);

public sealed record UpsertDesignTokensRequest
{
    public string Theme { get; init; } = "light";
    public int? BaseWidth { get; init; }
    public IDictionary<string, decimal>? Spacing { get; init; }
    public IDictionary<string, decimal>? Typography { get; init; }
    public IDictionary<string, decimal>? Radius { get; init; }
    public IDictionary<string, string>? Colors { get; init; }
    public IDictionary<string, decimal>? IconSizes { get; init; }
    public IDictionary<string, decimal>? MotionDurations { get; init; }
    public string? Notes { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalCategories { get; init; }
}

public sealed record PublishDesignTokenVersionRequest(string? Version, string? Notes);

public sealed record RollbackDesignTokenVersionRequest(string? TargetVersion, string? Reason);

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MathLearning.Application.DTOs.Users;

public record UserSettingsDto(
    string UserId,
    string Language,
    string? LanguageCode,
    string Theme,
    bool HintsEnabled,
    bool SoundEnabled,
    bool VibrationEnabled,
    bool DailyNotificationEnabled,
    string? DailyNotificationTime
);

public sealed class UpdateUserSettingsRequest
{
    public string? Language { get; init; }
    public string? LanguageCode { get; init; }
    public string? Theme { get; init; }
    public bool? HintsEnabled { get; init; }
    public bool? SoundEnabled { get; init; }
    public bool? VibrationEnabled { get; init; }
    public bool? DailyNotificationEnabled { get; init; }
    public string? DailyNotificationTime { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }

    public string? ResolveLanguageCode()
    {
        if (!string.IsNullOrWhiteSpace(LanguageCode))
        {
            return LanguageCode;
        }

        if (ExtensionData is null ||
            !ExtensionData.TryGetValue("language_code", out var languageCodeElement) ||
            languageCodeElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var languageCode = languageCodeElement.GetString();
        return string.IsNullOrWhiteSpace(languageCode) ? null : languageCode;
    }
}

namespace MathLearning.Application.DTOs.Users;

public record UserSettingsDto(
    int UserId,
    string Language,
    string Theme,
    bool HintsEnabled,
    bool SoundEnabled,
    bool VibrationEnabled,
    bool DailyNotificationEnabled,
    string? DailyNotificationTime
);

public record UpdateUserSettingsRequest(
    string? Language,
    string? Theme,
    bool? HintsEnabled,
    bool? SoundEnabled,
    bool? VibrationEnabled,
    bool? DailyNotificationEnabled,
    string? DailyNotificationTime
);

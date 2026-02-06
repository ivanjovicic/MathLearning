namespace MathLearning.Domain.Entities;

public class UserSettings
{
    public int Id { get; set; }
    public int UserId { get; set; }

    public string Language { get; set; } = "sr";
    public string Theme { get; set; } = "light";
    public bool HintsEnabled { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public bool VibrationEnabled { get; set; } = true;
    public bool DailyNotificationEnabled { get; set; } = false;
    public string? DailyNotificationTime { get; set; } = "18:00";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
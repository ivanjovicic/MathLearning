namespace MathLearning.Infrastructure.Services;

public sealed class XpTrackingOptions
{
    public const string SectionName = "XpTracking";

    public int DailyXpCap { get; set; } = 500;
    public int WeeklyXpCap { get; set; } = 2_000;
    public int MonthlyXpCap { get; set; } = 6_000;
    public int HintPenaltyPercent { get; set; } = 25;
    public int HintPenaltyFlat { get; set; } = 0;
    public bool EnableAntiCheat { get; set; } = true;
    public bool EnableXpCaps { get; set; } = true;
}

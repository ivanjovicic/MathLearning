namespace MathLearning.Domain.Entities;

public class EconomyRewardDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RewardIdPattern { get; set; } = string.Empty;
    public string RewardType { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? EligibilityRuleJson { get; set; }
    public string GrantRuleJson { get; set; } = "{}";
    public string IneligibilityMessage { get; set; } = "Reward is not eligible.";
    public bool IsSingleUse { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? MetadataJson { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
namespace MathLearning.Domain.Entities;

public class AdminEconomyRewardGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string GrantId { get; set; } = string.Empty;
    public Guid EconomyTransactionId { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public int Coins { get; set; }
    public int Xp { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
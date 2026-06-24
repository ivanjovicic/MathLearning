namespace MathLearning.Domain.Entities;

public class DailyRunChestClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public DateOnly Day { get; set; }
    public DateOnly Date
    {
        get => Day;
        set => Day = value;
    }
    public string TransactionId { get; set; } = string.Empty;
    public int Xp { get; set; }
    public int Coins { get; set; }
    public string CosmeticFragment { get; set; } = string.Empty;
    public int FragmentCopies { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

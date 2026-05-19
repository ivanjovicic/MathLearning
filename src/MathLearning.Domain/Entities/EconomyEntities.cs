namespace MathLearning.Domain.Entities;

public class UserSeasonProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public int SeasonId { get; set; }
    public int EarnedXp { get; set; }
    public int Level { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public CosmeticSeason Season { get; set; } = null!;
}

public class UserSeasonDailyRunClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public int SeasonId { get; set; }
    public string DailyRunTransactionId { get; set; } = string.Empty;
    public Guid? DailyRunClaimId { get; set; }
    public int AwardedXp { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public CosmeticSeason Season { get; set; } = null!;
}

public class UserSeasonMilestoneClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public int SeasonId { get; set; }
    public int MilestoneId { get; set; }
    public string RewardType { get; set; } = string.Empty;
    public int? CoinsAwarded { get; set; }
    public int? XpAwarded { get; set; }
    public int? CosmeticItemId { get; set; }
    public string? FragmentName { get; set; }
    public int? FragmentCopiesAwarded { get; set; }
    public bool AlreadyOwned { get; set; }
    public DateTime ClaimedAtUtc { get; set; } = DateTime.UtcNow;

    public CosmeticSeason Season { get; set; } = null!;
}

public class UserCosmeticFragmentProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string FragmentName { get; set; } = string.Empty;
    public int Copies { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

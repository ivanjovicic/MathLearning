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
    public int CosmeticItemId { get; set; }
    public int Collected { get; set; }
    public int Required { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UnlockedAtUtc { get; set; }

    public CosmeticItem CosmeticItem { get; set; } = null!;
}

public class CosmeticsIdempotencyLedger
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string RequestJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public string? ErrorCode { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

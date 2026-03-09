namespace MathLearning.Domain.Entities;

public class SchoolScoreAggregate
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public string Period { get; set; } = "week";
    public DateTime PeriodStartUtc { get; set; }
    public int XpTotal { get; set; }
    public int ActiveStudents { get; set; }
    public int EligibleStudents { get; set; }
    public decimal AverageXpPerActiveStudent { get; set; }
    public decimal ParticipationRate { get; set; }
    public decimal CompositeScore { get; set; }
    public int Rank { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Fair ranking metric: XpTotal / sqrt(EligibleStudents). Normalises for school size.</summary>
    public double WeightedXp { get; set; }

    /// <summary>Optional competition season this aggregate belongs to.</summary>
    public Guid? SeasonId { get; set; }

    public School? School { get; set; }
    public CompetitionSeason? Season { get; set; }
}

public class SchoolRankHistory
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public string Period { get; set; } = "week";
    public DateTime PeriodStartUtc { get; set; }
    public int Rank { get; set; }
    public int XpTotal { get; set; }
    public int ActiveStudents { get; set; }
    public decimal ParticipationRate { get; set; }
    public decimal CompositeScore { get; set; }
    public DateTime SnapshotTimeUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Fair ranking metric snapshot: XpTotal / sqrt(EligibleStudents) at time of snapshot.</summary>
    public double WeightedXp { get; set; }

    /// <summary>Optional competition season this history snapshot belongs to.</summary>
    public Guid? SeasonId { get; set; }

    public School? School { get; set; }
    public CompetitionSeason? Season { get; set; }
}

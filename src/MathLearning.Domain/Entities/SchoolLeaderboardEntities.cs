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

    public School? School { get; set; }
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

    public School? School { get; set; }
}

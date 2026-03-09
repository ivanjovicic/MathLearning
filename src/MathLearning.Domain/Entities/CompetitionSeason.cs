namespace MathLearning.Domain.Entities;

/// <summary>
/// Competition season that isolates school leaderboard rankings.
/// Each season has its own aggregate, history, and XP event records.
/// SeasonId is nullable on related tables so existing data is unaffected.
/// </summary>
public class CompetitionSeason
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

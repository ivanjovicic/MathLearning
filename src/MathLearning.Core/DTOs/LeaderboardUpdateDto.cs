namespace MathLearning.Core.DTOs;

public record LeaderboardUpdateDto
{
    public string Scope { get; init; } = "global";
    public string Period { get; init; } = "all_time";
    public string UserId { get; init; } = string.Empty;
    public int XpDelta { get; init; }
    public int? SchoolId { get; init; }
    public int? FacultyId { get; init; }
}

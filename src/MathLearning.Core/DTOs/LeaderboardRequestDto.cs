namespace MathLearning.Core.DTOs;

public record LeaderboardRequestDto
{
    public string Scope { get; init; } = "global"; // global | school | faculty | friends
    public string Period { get; init; } = "all_time"; // all_time | week | month | day
    public int Limit { get; init; } = 50;
    public string? Cursor { get; init; }
    public string? UserId { get; init; }
    public int? SchoolId { get; init; }
    public int? FacultyId { get; init; }
    public List<string>? FriendIds { get; init; }
}

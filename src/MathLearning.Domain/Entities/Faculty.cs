namespace MathLearning.Domain.Entities;

/// <summary>
/// Represents a university faculty for aggregated leaderboards
/// </summary>
public class Faculty
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? University { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

namespace MathLearning.Domain.Entities;

/// <summary>
/// Represents an educational school for aggregated leaderboards
/// </summary>
public class School
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

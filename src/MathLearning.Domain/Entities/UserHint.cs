namespace MathLearning.Domain.Entities;

public class UserHint
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int QuestionId { get; set; }
    public string HintType { get; set; } = string.Empty; // "formula", "clue", "solution"
    public DateTime UsedAt { get; set; }
    
    // Navigation properties
    public Question? Question { get; set; }
}

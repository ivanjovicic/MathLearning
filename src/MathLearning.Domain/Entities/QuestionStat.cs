namespace MathLearning.Domain.Entities;

public class QuestionStat
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;
    public int QuestionId { get; set; }

    public DateTime? LastAnswered { get; set; }
    public int SuccessStreak { get; set; } = 0;
    public double Ease { get; set; } = 1.3;
    public DateTime NextReview { get; set; } = DateTime.UtcNow;

    public Question Question { get; set; } = null!;
}

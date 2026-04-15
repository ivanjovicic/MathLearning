namespace MathLearning.Domain.Entities;

public class UserAnswerAudit
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int QuestionId { get; set; }

    public string Source { get; set; } = "quiz_answer";
    public bool IsOffline { get; set; }
    public string? ClientId { get; set; }

    public string Answer { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }
    public bool IsFirstTimeCorrect { get; set; }
    public string Reason { get; set; } = "not_eligible";

    public int AwardedXp { get; set; }
    public int TotalXpAfterAward { get; set; }

    public DateTime AnsweredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

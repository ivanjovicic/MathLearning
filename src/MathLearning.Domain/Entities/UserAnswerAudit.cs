using System;

namespace MathLearning.Domain.Entities;

public class UserAnswerAudit
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int QuestionId { get; set; }

    public string Answer { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public int AwardedXp { get; set; }

    public DateTime AnsweredAt { get; set; }
}

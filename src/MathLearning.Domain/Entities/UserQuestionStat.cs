using System;

namespace MathLearning.Domain.Entities;

public class UserQuestionStat
{
    public string UserId { get; set; } = string.Empty;
    public int QuestionId { get; set; }

    public int Attempts { get; set; }
    public int CorrectAttempts { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public Question? Question { get; set; }
}

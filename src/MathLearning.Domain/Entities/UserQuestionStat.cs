using System;

namespace MathLearning.Domain.Entities;

public class UserQuestionStat
{
    public int UserId { get; set; }
    public int QuestionId { get; set; }

    public int Attempts { get; set; }
    public int CorrectAttempts { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public Question? Question { get; set; }
}

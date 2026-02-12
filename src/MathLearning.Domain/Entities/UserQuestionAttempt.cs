using System;

namespace MathLearning.Domain.Entities;

public class UserQuestionAttempt
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int QuestionId { get; set; }

    public DateTime AttemptedAt { get; set; }
}

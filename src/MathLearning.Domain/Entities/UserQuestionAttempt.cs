using System;

namespace MathLearning.Domain.Entities;

public class UserQuestionAttempt
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int QuestionId { get; set; }

    public DateTime AttemptedAt { get; set; }
}

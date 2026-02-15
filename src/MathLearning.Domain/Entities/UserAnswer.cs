using System;

namespace MathLearning.Domain.Entities;

public class UserAnswer
{
    public int Id { get; private set; }
    public string UserId { get; set; } = string.Empty;
    public int QuestionId { get; set; }
    public Guid QuizSessionId { get; set; }
    public string Answer { get; set; } = "";
    public bool IsCorrect { get; set; }
    public int TimeSpentSeconds { get; set; }
    public DateTime AnsweredAt { get; set; }

    public Question? Question { get; set; }
    public QuizSession? QuizSession { get; set; }
}

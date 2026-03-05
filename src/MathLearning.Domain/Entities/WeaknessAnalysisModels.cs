namespace MathLearning.Domain.Entities;

public static class WeaknessLevels
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
}

public class QuizAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid QuizId { get; set; }
    public int QuestionId { get; set; }
    public int TopicId { get; set; }
    public int SubtopicId { get; set; }
    public bool Correct { get; set; }
    public int TimeSpentMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class UserTopicStat
{
    public Guid UserId { get; set; }
    public int TopicId { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public decimal Accuracy { get; set; }
    public DateTime LastAttempt { get; set; } = DateTime.UtcNow;
    public decimal WeaknessScore { get; set; }
}

public class UserSubtopicStat
{
    public Guid UserId { get; set; }
    public int SubtopicId { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public decimal Accuracy { get; set; }
    public DateTime LastAttempt { get; set; } = DateTime.UtcNow;
    public decimal WeaknessScore { get; set; }
}

public class UserWeakness
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public int? TopicId { get; set; }
    public int? SubtopicId { get; set; }
    public string WeaknessLevel { get; set; } = WeaknessLevels.Low;
    public decimal Confidence { get; set; }
    public string RecommendedPractice { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

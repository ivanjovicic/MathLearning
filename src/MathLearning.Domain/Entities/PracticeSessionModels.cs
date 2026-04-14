namespace MathLearning.Domain.Entities;

public static class PracticeSessionStatuses
{
    public const string Active = "Active";
    public const string Completed = "Completed";
    public const string Abandoned = "Abandoned";
}

public static class PracticeDifficulties
{
    public const string Easy = "easy";
    public const string Medium = "medium";
    public const string Hard = "hard";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Medium;

        return value.Trim().ToLowerInvariant() switch
        {
            Easy => Easy,
            Medium => Medium,
            Hard => Hard,
            _ => Medium
        };
    }
}

public class PracticeSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public int? TopicId { get; set; }
    public int? SubtopicId { get; set; }
    public string? SkillNodeId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = PracticeSessionStatuses.Active;
    public int TargetQuestions { get; set; } = 10;
    public int AnsweredQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public int XpEarned { get; set; }
    public string RecommendedDifficulty { get; set; } = PracticeDifficulties.Medium;
    public decimal InitialMastery { get; set; }
    public decimal? FinalMastery { get; set; }

    public List<PracticeSessionItem> Items { get; set; } = new();
}

public class PracticeSessionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int QuestionId { get; set; }
    public int TopicId { get; set; }
    public int SubtopicId { get; set; }
    public string Difficulty { get; set; } = PracticeDifficulties.Medium;
    public DateTime PresentedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AnsweredAt { get; set; }
    public bool? Correct { get; set; }
    public int? TimeSpentMs { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public decimal BktPrior { get; set; }
    public decimal BktPosterior { get; set; }

    public PracticeSession? Session { get; set; }
}

public class MasteryState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public int TopicId { get; set; }
    public int? SubtopicId { get; set; }
    public decimal PL { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

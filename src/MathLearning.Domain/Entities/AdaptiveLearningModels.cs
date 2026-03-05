namespace MathLearning.Domain.Entities;

public static class AdaptiveDifficultyLevels
{
    public const string Easy = "Easy";
    public const string Medium = "Medium";
    public const string Hard = "Hard";

    public static readonly string[] Ordered = [Easy, Medium, Hard];

    public static string Normalize(string? difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
            return Medium;

        return difficulty.Trim().ToLowerInvariant() switch
        {
            "easy" => Easy,
            "medium" => Medium,
            "hard" => Hard,
            _ => Medium
        };
    }
}

public class UserLearningProfile
{
    public string UserId { get; set; } = string.Empty;
    public string PreferredDifficulty { get; set; } = AdaptiveDifficultyLevels.Medium;

    public double RollingAccuracy { get; set; }
    public double RollingAverageResponseSeconds { get; set; }
    public int RollingWindowSize { get; set; } = 20;

    public int TotalAttempts { get; set; }
    public int TotalCorrect { get; set; }

    public DateTime? LastPracticeAt { get; set; }
    public DateTime? LastDifficultyChangeAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class UserTopicMastery
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;
    public int TopicId { get; set; }

    public double MasteryScore { get; set; }
    public int Attempts { get; set; }
    public int CorrectAttempts { get; set; }
    public double AverageConfidence { get; set; }

    public string DifficultyLevel { get; set; } = AdaptiveDifficultyLevels.Medium;

    public bool IsWeak { get; set; }
    public DateTime? WeakDetectedAt { get; set; }
    public DateTime? LastPracticedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Topic? Topic { get; set; }
}

public class UserQuestionHistory
{
    public long Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public int QuestionId { get; set; }
    public int TopicId { get; set; }
    public int SubtopicId { get; set; }

    public bool IsCorrect { get; set; }
    public double Confidence { get; set; }
    public int ResponseTimeSeconds { get; set; }

    public string DifficultyLevel { get; set; } = AdaptiveDifficultyLevels.Medium;

    public DateTime AnsweredAt { get; set; }

    public Guid? AdaptiveSessionId { get; set; }
    public Guid? AdaptiveSessionItemId { get; set; }

    public Question? Question { get; set; }
    public Topic? Topic { get; set; }
    public Subtopic? Subtopic { get; set; }
}

public class ReviewSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;
    public int QuestionId { get; set; }
    public int TopicId { get; set; }

    public double EasinessFactor { get; set; } = 2.5;
    public int IntervalDays { get; set; } = 1;
    public int RepetitionCount { get; set; }

    public DateTime DueAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastReviewedAt { get; set; }
    public bool? LastWasCorrect { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Question? Question { get; set; }
    public Topic? Topic { get; set; }
}

public class AdaptiveSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(35);

    public bool IsCompleted { get; set; }
    public string ProfileDifficulty { get; set; } = AdaptiveDifficultyLevels.Medium;

    public List<AdaptiveSessionItem> Items { get; set; } = new();
}

public class AdaptiveSessionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AdaptiveSessionId { get; set; }
    public int QuestionId { get; set; }

    public int TopicId { get; set; }
    public int SubtopicId { get; set; }

    public string SourceType { get; set; } = "recent";
    public string DifficultyLevel { get; set; } = AdaptiveDifficultyLevels.Medium;

    public int Sequence { get; set; }

    public bool? IsCorrect { get; set; }
    public double? Confidence { get; set; }
    public int? ResponseTimeSeconds { get; set; }
    public DateTime? AnsweredAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AdaptiveSession? AdaptiveSession { get; set; }
    public Question? Question { get; set; }
    public Topic? Topic { get; set; }
    public Subtopic? Subtopic { get; set; }
}

public class AdaptiveRecommendation
{
    public int TopicId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Difficulty { get; set; } = AdaptiveDifficultyLevels.Medium;
    public int QuestionCount { get; set; }
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class AdaptiveAnswerRequest
{
    public Guid AdaptiveSessionId { get; set; }
    public Guid AdaptiveSessionItemId { get; set; }
    public int QuestionId { get; set; }
    public string Answer { get; set; } = string.Empty;
    public int ResponseTimeSeconds { get; set; }
    public double Confidence { get; set; } = 0.5;
    public DateTime? AnsweredAt { get; set; }
}

public class AdaptiveAnswerResult
{
    public bool IsCorrect { get; set; }
    public string DifficultyLevel { get; set; } = AdaptiveDifficultyLevels.Medium;
    public bool WasDifficultyAdjusted { get; set; }

    public int TopicId { get; set; }
    public double TopicMasteryScore { get; set; }
    public bool IsWeakTopic { get; set; }

    public DateTime NextReviewAt { get; set; }
    public int ReviewIntervalDays { get; set; }
    public double ReviewEasinessFactor { get; set; }

    public string? Explanation { get; set; }
}

public class ReviewItem
{
    public int QuestionId { get; set; }
    public int TopicId { get; set; }
    public string Topic { get; set; } = string.Empty;

    public DateTime DueAt { get; set; }
    public int IntervalDays { get; set; }
    public int RepetitionCount { get; set; }
    public double EasinessFactor { get; set; }

    public string Difficulty { get; set; } = AdaptiveDifficultyLevels.Medium;
    public bool Overdue { get; set; }
}

public class TopicMastery
{
    public int TopicId { get; set; }
    public string Topic { get; set; } = string.Empty;

    public double MasteryScore { get; set; }
    public int Attempts { get; set; }
    public double Accuracy { get; set; }

    public bool IsWeak { get; set; }
    public string Difficulty { get; set; } = AdaptiveDifficultyLevels.Medium;
    public DateTime? LastPracticedAt { get; set; }
}

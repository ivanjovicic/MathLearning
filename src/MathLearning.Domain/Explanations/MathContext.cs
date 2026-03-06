namespace MathLearning.Domain.Explanations;

public sealed class MathContext
{
    public string Topic { get; }
    public string Subtopic { get; }
    public int Grade { get; }
    public DifficultyLevel Difficulty { get; }
    public CommonMistakeType CommonMistakeType { get; }

    public MathContext(
        string topic,
        string subtopic,
        int grade,
        DifficultyLevel difficulty,
        CommonMistakeType commonMistakeType = CommonMistakeType.None)
    {
        Topic = string.IsNullOrWhiteSpace(topic) ? throw new ArgumentException("Topic is required.", nameof(topic)) : topic.Trim();
        Subtopic = string.IsNullOrWhiteSpace(subtopic) ? throw new ArgumentException("Subtopic is required.", nameof(subtopic)) : subtopic.Trim();
        Grade = grade < 0 ? throw new ArgumentOutOfRangeException(nameof(grade), "Grade must be non-negative.") : grade;
        Difficulty = difficulty;
        CommonMistakeType = commonMistakeType;
    }
}

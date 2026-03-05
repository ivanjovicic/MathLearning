namespace MathLearning.Application.Services;

public sealed record QuestionSelectionCriteria(
    int? TopicId,
    int? SubtopicId,
    string Difficulty,
    IReadOnlyCollection<int> ExcludedQuestionIds,
    int Take = 1);

public sealed record SelectedQuestion(
    int Id,
    string Prompt,
    IReadOnlyList<SelectedQuestionOption> Options,
    int TopicId,
    int SubtopicId,
    string Difficulty,
    string? CorrectAnswer);

public sealed record SelectedQuestionOption(
    int Id,
    string Text,
    bool IsCorrect);

public interface IQuestionSelector
{
    Task<SelectedQuestion?> GetNextQuestionAsync(
        QuestionSelectionCriteria criteria,
        CancellationToken ct = default);
}

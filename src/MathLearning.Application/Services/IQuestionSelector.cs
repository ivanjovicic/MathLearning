using MathLearning.Domain.Enums;

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
    string? CorrectAnswer,
    ContentFormat PromptFormat = ContentFormat.MarkdownWithMath,
    RenderMode RenderMode = RenderMode.Auto,
    string? SemanticsAltText = null);

public sealed record SelectedQuestionOption(
    int Id,
    string Text,
    bool IsCorrect,
    ContentFormat TextFormat = ContentFormat.MarkdownWithMath,
    RenderMode RenderMode = RenderMode.Auto,
    string? SemanticsAltText = null);

public interface IQuestionSelector
{
    Task<SelectedQuestion?> GetNextQuestionAsync(
        QuestionSelectionCriteria criteria,
        CancellationToken ct = default);
}

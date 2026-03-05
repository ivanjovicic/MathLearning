using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Services;

public sealed class EfQuestionSelector : IQuestionSelector
{
    private readonly ApiDbContext _db;

    public EfQuestionSelector(ApiDbContext db)
    {
        _db = db;
    }

    public async Task<SelectedQuestion?> GetNextQuestionAsync(
        QuestionSelectionCriteria criteria,
        CancellationToken ct = default)
    {
        var excluded = criteria.ExcludedQuestionIds?.Distinct().ToList() ?? [];
        var normalizedDifficulty = PracticeDifficulties.Normalize(criteria.Difficulty);
        var targetDifficulty = DifficultyToNumeric(normalizedDifficulty);

        var baseQuery =
            from q in _db.Questions.AsNoTracking()
                .Include(x => x.Options)
            join s in _db.Subtopics.AsNoTracking()
                on q.SubtopicId equals s.Id
            where (!criteria.SubtopicId.HasValue || q.SubtopicId == criteria.SubtopicId.Value)
                && (!criteria.TopicId.HasValue || s.TopicId == criteria.TopicId.Value)
                && !excluded.Contains(q.Id)
            select new
            {
                Question = q,
                TopicId = s.TopicId,
                Distance = Math.Abs(q.Difficulty - targetDifficulty)
            };

        var candidate = await baseQuery
            .OrderBy(x => x.Distance)
            .ThenBy(x => Guid.NewGuid())
            .FirstOrDefaultAsync(ct);

        if (candidate is null)
            return null;

        return new SelectedQuestion(
            candidate.Question.Id,
            candidate.Question.Text,
            candidate.Question.Options
                .OrderBy(o => o.Id)
                .Select(o => new SelectedQuestionOption(o.Id, o.Text, o.IsCorrect))
                .ToList(),
            candidate.TopicId,
            candidate.Question.SubtopicId,
            NumericToDifficulty(candidate.Question.Difficulty),
            candidate.Question.CorrectAnswer);
    }

    private static int DifficultyToNumeric(string difficulty) =>
        difficulty switch
        {
            PracticeDifficulties.Easy => 2,
            PracticeDifficulties.Medium => 3,
            PracticeDifficulties.Hard => 4,
            _ => 3
        };

    private static string NumericToDifficulty(int numericDifficulty) =>
        numericDifficulty switch
        {
            <= 2 => PracticeDifficulties.Easy,
            3 => PracticeDifficulties.Medium,
            _ => PracticeDifficulties.Hard
        };
}

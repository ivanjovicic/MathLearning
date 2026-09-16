using MathLearning.Domain.Entities;

namespace MathLearning.Infrastructure.Services;

/// <summary>
/// Shared publish/content gate for every user-facing question selector.
/// The predicate uses canonical option metadata, never localized titles.
/// </summary>
public static class PlayableQuestionQuery
{
    public static IQueryable<Question> WherePlayable(this IQueryable<Question> query)
    {
        return query.Where(q =>
            q.PublishState == QuestionPublishStates.Published &&
            !q.IsDeleted &&
            q.Text.Trim() != string.Empty &&
            q.Options.Count(o => o.Text.Trim() != string.Empty) >= 2 &&
            q.Options.Count(o => o.IsCorrect) == 1);
    }
}

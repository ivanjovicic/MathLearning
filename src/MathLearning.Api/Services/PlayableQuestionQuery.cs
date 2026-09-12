using MathLearning.Domain.Entities;

namespace MathLearning.Api.Services;

/// Keeps every user-facing question selector on the same publish/content gate.
/// The predicate is based on canonical option metadata, never on a localized
/// title or a client-provided answer key.
public static class PlayableQuestionQuery
{
    public static IQueryable<Question> WherePlayable(
        this IQueryable<Question> query)
    {
        return query.Where(q =>
            q.PublishState == QuestionPublishStates.Published &&
            !q.IsDeleted &&
            q.Text.Trim() != string.Empty &&
            q.Options.Count(o => o.Text.Trim() != string.Empty) >= 2 &&
            q.Options.Count(o => o.IsCorrect) == 1);
    }
}

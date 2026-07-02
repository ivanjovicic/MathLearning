using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MathLearning.Infrastructure.Services.QuestionAuthoring;

internal static class QuestionAuthoringConcurrencySupport
{
    public const int MaxVersionAllocationAttempts = 3;

    public static bool IsVersionNumberConflict(DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return postgres.ConstraintName is "UX_question_drafts_question_version"
                or "UX_question_versions_question_version"
                or "UX_question_versions_source_draft";
        }

        return exception.Entries.Count > 0
            && exception.Entries.Any(entry =>
                entry.Entity is QuestionDraft or QuestionVersion);
    }
}

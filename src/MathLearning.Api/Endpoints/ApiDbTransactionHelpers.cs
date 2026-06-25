using System.Data;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MathLearning.Api.Endpoints;

internal static class ApiDbTransactionHelpers
{
    public static async Task<T> ExecuteWithSerializableRetryAsync<T>(
        ApiDbContext db,
        ILogger logger,
        Func<Task<T>> action,
        CancellationToken cancellationToken,
        string? uniqueViolationConstraintName = null)
    {
        if (!db.Database.IsRelational())
        {
            db.ChangeTracker.Clear();
            var inMemoryResult = await action();
            await db.SaveChangesAsync(cancellationToken);
            return inMemoryResult;
        }

        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await action();
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxAttempts)
            {
                await tx.RollbackAsync(cancellationToken);
                logger.LogWarning(
                    ex,
                    "Retrying serializable transaction after EF concurrency conflict. Attempt={Attempt}/{MaxAttempts}",
                    attempt,
                    maxAttempts);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure && attempt < maxAttempts)
            {
                await tx.RollbackAsync(cancellationToken);
                logger.LogWarning(
                    ex,
                    "Retrying serializable transaction after PostgreSQL serialization failure. Attempt={Attempt}/{MaxAttempts}",
                    attempt,
                    maxAttempts);
            }
            catch (PostgresException ex) when (
                uniqueViolationConstraintName is not null &&
                ex.SqlState == PostgresErrorCodes.UniqueViolation &&
                string.Equals(ex.ConstraintName, uniqueViolationConstraintName, StringComparison.Ordinal) &&
                attempt < maxAttempts)
            {
                await tx.RollbackAsync(cancellationToken);
                logger.LogWarning(
                    ex,
                    "Retrying transaction after uniqueness conflict on {ConstraintName}. Attempt={Attempt}/{MaxAttempts}",
                    uniqueViolationConstraintName,
                    attempt,
                    maxAttempts);
            }
        }

        throw new InvalidOperationException("Failed to process transaction after max retries.");
    }
}

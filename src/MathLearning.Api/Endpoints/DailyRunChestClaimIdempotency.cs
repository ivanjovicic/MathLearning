using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

/// <summary>
/// Domain-table idempotency for Daily Run chest claim (Policy B).
/// Authority: <c>daily_run_chest_claims</c> unique indexes on (userId, transactionId) and (userId, day).
/// Mobile <c>transactionId</c> is the settlement root; client <c>idempotencyKey</c> is not hashed here.
/// </summary>
internal static class DailyRunChestClaimIdempotency
{
    internal enum ResolutionKind
    {
        ProcessNew,
        ReplayByTransaction,
        ReplayByDay
    }

    internal sealed record Resolution(ResolutionKind Kind, DailyRunChestClaim? ExistingClaim);

    internal static async Task<Resolution> ResolveAsync(
        ApiDbContext db,
        string userId,
        DateOnly day,
        string transactionId,
        CancellationToken cancellationToken)
    {
        var byTransaction = await db.DailyRunChestClaims
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.TransactionId == transactionId,
                cancellationToken);
        if (byTransaction is not null)
            return new Resolution(ResolutionKind.ReplayByTransaction, byTransaction);

        var byDay = await db.DailyRunChestClaims
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.Day == day,
                cancellationToken);
        if (byDay is not null)
            return new Resolution(ResolutionKind.ReplayByDay, byDay);

        return new Resolution(ResolutionKind.ProcessNew, null);
    }
}

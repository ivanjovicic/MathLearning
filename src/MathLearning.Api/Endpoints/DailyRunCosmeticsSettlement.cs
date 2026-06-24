using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

internal static class DailyRunCosmeticsSettlement
{
    public static bool IsDailyRunSource(string normalizedSource)
        => normalizedSource is "dailyrun" or "daily_run";

    public static string? ResolveTransactionId(string? requestTransactionId, string operationId)
        => string.IsNullOrWhiteSpace(requestTransactionId) ? operationId : requestTransactionId.Trim();

    public static async Task<(string FragmentName, int Copies, IResult? Error)> ResolveFragmentGrantAsync(
        ApiDbContext db,
        string userId,
        string transactionId,
        CancellationToken ct)
    {
        var chestClaim = await db.DailyRunChestClaims
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.TransactionId == transactionId, ct);
        if (chestClaim is null)
        {
            return (string.Empty, 0, Results.Conflict(
                EconomyEndpointHelpers.BusinessError(
                    "not_eligible",
                    "Daily Run chest claim was not found for this transactionId.")));
        }

        var seasonSettled = await db.UserSeasonDailyRunClaims
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.DailyRunTransactionId == transactionId, ct);
        if (!seasonSettled)
        {
            return (string.Empty, 0, Results.Conflict(
                EconomyEndpointHelpers.BusinessError(
                    "not_eligible",
                    "Season daily run settlement must complete before fragment grant.")));
        }

        if (string.IsNullOrWhiteSpace(chestClaim.CosmeticFragment))
        {
            return (string.Empty, 0, Results.Conflict(
                EconomyEndpointHelpers.BusinessError(
                    "invalid_reward_payload",
                    "Daily Run chest claim has no cosmetic fragment reward.")));
        }

        var copies = chestClaim.FragmentCopies;
        if (copies <= 0)
            copies = 1;

        return (chestClaim.CosmeticFragment, copies, null);
    }

    public static async Task<SeasonDailyRunFragmentGrantHint?> BuildFragmentGrantHintAsync(
        ApiDbContext db,
        string userId,
        string transactionId,
        CancellationToken ct)
    {
        var chestClaim = await db.DailyRunChestClaims
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.TransactionId == transactionId, ct);
        if (chestClaim is null || string.IsNullOrWhiteSpace(chestClaim.CosmeticFragment))
            return null;

        var copies = chestClaim.FragmentCopies;
        if (copies <= 0)
            copies = 1;

        return new SeasonDailyRunFragmentGrantHint(
            transactionId,
            chestClaim.CosmeticFragment,
            copies);
    }
}

public sealed record SeasonDailyRunFragmentGrantHint(
    string TransactionId,
    string FragmentName,
    int Copies);

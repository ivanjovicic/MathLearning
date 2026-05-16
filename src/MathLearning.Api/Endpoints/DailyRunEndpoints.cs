using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class DailyRunEndpoints
{
    private static readonly string[] CosmeticFragments =
    {
        "Comet Frame Fragment",
        "Nova Trail Fragment",
        "Neon Number Burst Fragment",
        "Solar Pulse Fragment"
    };

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ClaimLocks = new();

    public static void MapDailyRunEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/daily-run")
            .RequireAuthorization()
            .WithTags("DailyRun");

        group.MapPost("/chest/claim", async (
            DailyRunChestClaimRequest request,
            ApiDbContext db,
            IXpTrackingService xpTrackingService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            if (!TryParseDay(request.Date, out var day))
            {
                return Results.BadRequest(new
                {
                    error = "Invalid date. Use YYYY-MM-DD.",
                    code = "INVALID_DATE"
                });
            }

            if (string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return Results.BadRequest(new
                {
                    error = "Invalid transactionId.",
                    code = "INVALID_TRANSACTION_ID"
                });
            }

            var lockKey = $"{userId}:{day:yyyy-MM-dd}";
            var gate = ClaimLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct);
            try
            {
                var existingClaimByTransaction = await db.DailyRunChestClaims
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.TransactionId == request.TransactionId, ct);
                if (existingClaimByTransaction is not null)
                {
                    var profileForTransactionRetry = await db.UserProfiles.AsNoTracking()
                        .FirstAsync(x => x.UserId == userId, ct);
                    return Results.Ok(BuildResponse(existingClaimByTransaction, profileForTransactionRetry, true));
                }

                var existingClaimForDay = await db.DailyRunChestClaims
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.Day == day, ct);
                if (existingClaimForDay is not null)
                {
                    var profileForDayRetry = await db.UserProfiles.AsNoTracking()
                        .FirstAsync(x => x.UserId == userId, ct);
                    return Results.Ok(BuildResponse(existingClaimForDay, profileForDayRetry, true));
                }

                var completedDailyRun = await db.UserDailyStats
                    .AsNoTracking()
                    .AnyAsync(x => x.UserId == userId && x.Day == day && x.Completed, ct);
                if (!completedDailyRun)
                {
                    return Results.Json(new
                    {
                        error = "Daily Run not completed for requested date.",
                        code = "DAILY_RUN_NOT_COMPLETED",
                        date = request.Date
                    }, statusCode: StatusCodes.Status409Conflict);
                }

                var reward = BuildCanonicalReward(userId, day);
                var nowUtc = DateTime.UtcNow;

                var useTransaction = db.Database.IsRelational();
                await using var tx = useTransaction
                    ? await db.Database.BeginTransactionAsync(ct)
                    : null;

                var profile = await xpTrackingService.AddXpAsync(
                    userId,
                    reward.Xp,
                    sourceType: "daily_run_chest",
                    sourceId: $"daily_run_chest:{day:yyyy-MM-dd}",
                    metadataJson: JsonSerializer.Serialize(new { day, request.TransactionId }),
                    ct: ct);

                profile.Coins += reward.Coins;
                profile.TotalCoinsEarned += reward.Coins;
                profile.UpdatedAt = nowUtc;

                var claim = new DailyRunChestClaim
                {
                    UserId = userId,
                    Day = day,
                    TransactionId = request.TransactionId,
                    Xp = reward.Xp,
                    Coins = reward.Coins,
                    CosmeticFragment = reward.CosmeticFragment,
                    CreatedAtUtc = nowUtc
                };

                db.DailyRunChestClaims.Add(claim);

                await db.SaveChangesAsync(ct);
                if (tx is not null)
                    await tx.CommitAsync(ct);

                return Results.Ok(BuildResponse(claim, profile, false));
            }
            catch (DbUpdateException)
            {
                var fallback = await db.DailyRunChestClaims
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.UserId == userId &&
                             (x.Day == day || x.TransactionId == request.TransactionId),
                        ct);

                if (fallback is not null)
                {
                    var profile = await db.UserProfiles.AsNoTracking()
                        .FirstAsync(x => x.UserId == userId, ct);
                    return Results.Ok(BuildResponse(fallback, profile, true));
                }

                throw;
            }
            finally
            {
                gate.Release();
            }
        })
        .WithName("ClaimDailyRunChest")
        .WithSummary("Claim daily run chest reward (server-authoritative and idempotent)");
    }

    private static bool TryParseDay(string? value, out DateOnly day)
    {
        day = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out day);
    }

    private static DailyRunChestReward BuildCanonicalReward(string userId, DateOnly day)
    {
        var seed = ComputeSeed(userId, day);
        var xp = 25 + (seed.XpSeed % 26);
        var coins = 8 + (seed.CoinsSeed % 13);
        var fragment = CosmeticFragments[seed.FragmentSeed % CosmeticFragments.Length];

        return new DailyRunChestReward(
            Xp: xp,
            Coins: coins,
            CosmeticFragment: fragment,
            FragmentCopies: 1);
    }

    private static DailyRunChestClaimResponse BuildResponse(
        DailyRunChestClaim claim,
        UserProfile profile,
        bool alreadyClaimed)
    {
        return new DailyRunChestClaimResponse(
            Success: true,
            Date: claim.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TransactionId: claim.TransactionId,
            AlreadyClaimed: alreadyClaimed,
            Reward: new DailyRunChestReward(
                Xp: claim.Xp,
                Coins: claim.Coins,
                CosmeticFragment: claim.CosmeticFragment,
                FragmentCopies: 1),
            Balances: new DailyRunChestBalances(
                Xp: profile.Xp,
                Level: profile.Level,
                Coins: profile.Coins));
    }

    private static (int XpSeed, int CoinsSeed, int FragmentSeed) ComputeSeed(
        string userId,
        DateOnly day)
    {
        var input = Encoding.UTF8.GetBytes($"{userId}|{day:yyyy-MM-dd}");
        var hash = SHA256.HashData(input);
        return (
            XpSeed: BinaryPrimitives.ReadInt32LittleEndian(hash.AsSpan(0, 4)) & int.MaxValue,
            CoinsSeed: BinaryPrimitives.ReadInt32LittleEndian(hash.AsSpan(4, 4)) & int.MaxValue,
            FragmentSeed: BinaryPrimitives.ReadInt32LittleEndian(hash.AsSpan(8, 4)) & int.MaxValue);
    }
}

public sealed record DailyRunChestClaimRequest(
    string? TransactionId,
    string? Date
);

public sealed record DailyRunChestClaimResponse(
    bool Success,
    string Date,
    string TransactionId,
    bool AlreadyClaimed,
    DailyRunChestReward Reward,
    DailyRunChestBalances Balances
);

public sealed record DailyRunChestReward(
    int Xp,
    int Coins,
    string CosmeticFragment,
    int FragmentCopies
);

public sealed record DailyRunChestBalances(
    int Xp,
    int Level,
    int Coins
);

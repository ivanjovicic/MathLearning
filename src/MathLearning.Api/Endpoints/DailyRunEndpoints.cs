using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class DailyRunEndpoints
{
    private static readonly Regex TransactionIdPattern =
        new("^daily_chest_tx_[A-Za-z0-9_-]{4,120}$", RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ClaimLocks = new();
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

            if (string.IsNullOrWhiteSpace(request.TransactionId) ||
                !TransactionIdPattern.IsMatch(request.TransactionId))
            {
                return Results.BadRequest(new
                {
                    error = "Invalid transactionId format.",
                    code = "INVALID_TRANSACTION_ID"
                });
            }

            var lockKey = $"{userId}:{day:yyyy-MM-dd}";
            var gate = ClaimLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct);
            try
            {
                var sameTx = await db.DailyRunChestClaims
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.TransactionId == request.TransactionId, ct);
                if (sameTx is not null)
                    return JsonSnapshot(sameTx.ResponseSnapshotJson);

                var existingClaimForDay = await db.DailyRunChestClaims
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.Day == day, ct);
                if (existingClaimForDay is not null)
                    return JsonSnapshot(existingClaimForDay.ResponseSnapshotJson);

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

                var reward = BuildCanonicalReward(day);
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

                var response = new DailyRunChestClaimResponse(
                    Success: true,
                    Date: day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    TransactionId: request.TransactionId,
                    AlreadyClaimed: false,
                    Reward: reward,
                    Balances: new DailyRunChestBalances(
                        Xp: profile.Xp,
                        Level: profile.Level,
                        Coins: profile.Coins));

                var rewardSnapshot = JsonSerializer.Serialize(reward, SnapshotJsonOptions);
                var responseSnapshot = JsonSerializer.Serialize(response, SnapshotJsonOptions);

                db.DailyRunChestClaims.Add(new DailyRunChestClaim
                {
                    UserId = userId,
                    Day = day,
                    TransactionId = request.TransactionId,
                    RewardSnapshotJson = rewardSnapshot,
                    ResponseSnapshotJson = responseSnapshot,
                    ClaimedAtUtc = nowUtc,
                    Status = "claimed",
                    ResultCode = "ok"
                });

                await db.SaveChangesAsync(ct);
                if (tx is not null)
                    await tx.CommitAsync(ct);

                return JsonSnapshot(responseSnapshot);
            }
            catch (DbUpdateException)
            {
                var fallback = await db.DailyRunChestClaims
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.UserId == userId &&
                        (x.TransactionId == request.TransactionId || x.Day == day), ct);

                if (fallback is not null)
                    return JsonSnapshot(fallback.ResponseSnapshotJson);

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

    private static IResult JsonSnapshot(string snapshot) =>
        Results.Text(snapshot, "application/json");

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

    private static DailyRunChestReward BuildCanonicalReward(DateOnly day)
    {
        // Canonical server-side reward; day-based deterministic variance can be expanded later.
        var bonus = day.DayOfWeek == DayOfWeek.Sunday ? 5 : 0;
        return new DailyRunChestReward(
            Xp: 40 + bonus,
            Coins: 12 + bonus / 2,
            CosmeticFragment: "Comet Frame Fragment",
            FragmentCopies: 1);
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

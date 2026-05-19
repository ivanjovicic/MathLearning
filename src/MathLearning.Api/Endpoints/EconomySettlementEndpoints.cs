using System.Text.Json;
using System.Text.Json.Nodes;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class EconomySettlementEndpoints
{
    private const int DailyFreeHintLimit = 10;
    private const int StreakFreezeUnitCost = 50;
    private const int MaxStreakFreezes = 5;

    private static readonly IReadOnlyDictionary<string, int> HintCosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["clue"] = 10,
        ["formula"] = 5,
        ["eliminate"] = 15,
        ["solution"] = 20
    };

    private static readonly IReadOnlyDictionary<string, (string ItemKey, int RequiredCopies)> FragmentUnlocks
        = new Dictionary<string, (string ItemKey, int RequiredCopies)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Comet Frame Fragment"] = ("comet-frame", 5),
            ["Nova Trail Fragment"] = ("nova-trail", 5),
            ["Neon Number Burst Fragment"] = ("neon-number-burst", 5),
            ["Solar Pulse Fragment"] = ("solar-pulse", 5)
        };

    public static void MapEconomySettlementEndpoints(this IEndpointRouteBuilder app)
    {
        var economy = app.MapGroup("/api/economy")
            .RequireAuthorization()
            .WithTags("Economy");

        economy.MapPost("/coins/spend", async (
            CoinSpendRequest request,
            ApiDbContext db,
            IEconomyTransactionService txService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = EndpointUser.GetUserId(ctx);
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            if (!ValidateIdempotencyKey(request.IdempotencyKey, out var keyError))
                return keyError!;
            if (request.Amount <= 0)
                return Results.BadRequest(BusinessError("invalid_amount", "Amount must be greater than zero."));

            var beginTuple = await TryBeginAsync(
                txService,
                userId,
                "economy_coins_spend",
                request.IdempotencyKey!,
                request,
                ct);
            if (beginTuple.Error is not null)
                return beginTuple.Error;
            var begin = beginTuple.Begin!;
            var idempotencyResult = HandleIdempotentDecision(begin);
            if (idempotencyResult is not null)
                return idempotencyResult;

            await using var dbTx = await BeginDbTransactionIfSupportedAsync(db, ct);
            var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
            if (profile is null)
            {
                var error = BusinessError("profile_not_found", "User profile not found.");
                await txService.FailAsync(begin.TransactionId, "profile_not_found", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            if (profile.Coins < request.Amount)
            {
                var error = BusinessError("insufficient_balance", "Insufficient coin balance.");
                await txService.FailAsync(begin.TransactionId, "insufficient_balance", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            profile.Coins -= request.Amount;
            profile.TotalCoinsSpent += request.Amount;
            profile.UpdatedAt = DateTime.UtcNow;

            var response = new CoinSpendResponse(
                Success: true,
                AlreadyProcessed: false,
                Coins: profile.Coins,
                FreeHints: await GetFreeHintsRemainingAsync(db, userId, ct),
                SpentCoins: request.Amount,
                ErrorCode: null,
                Message: null);

            await txService.CompleteAsync(begin.TransactionId, response, ct);
            if (dbTx is not null) await dbTx.CommitAsync(ct);
            return Results.Ok(response);
        });

        economy.MapPost("/hints/use", async (
            HintUseRequest request,
            ApiDbContext db,
            IEconomyTransactionService txService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = EndpointUser.GetUserId(ctx);
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            if (!ValidateIdempotencyKey(request.IdempotencyKey, out var keyError))
                return keyError!;
            if (request.QuestionId <= 0)
                return Results.BadRequest(BusinessError("invalid_question_id", "QuestionId must be greater than zero."));

            var hintType = Normalize(request.HintType);
            if (!HintCosts.TryGetValue(hintType, out var serverCost))
                return Results.BadRequest(BusinessError("invalid_hint_type", "Unsupported hint type."));

            var beginTuple = await TryBeginAsync(
                txService,
                userId,
                "economy_hint_use",
                request.IdempotencyKey!,
                request with { HintType = hintType, CostCoins = serverCost },
                ct);
            if (beginTuple.Error is not null)
                return beginTuple.Error;
            var begin = beginTuple.Begin!;
            var idempotencyResult = HandleIdempotentDecision(begin);
            if (idempotencyResult is not null)
                return idempotencyResult;

            await using var dbTx = await BeginDbTransactionIfSupportedAsync(db, ct);
            var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
            if (profile is null)
            {
                var error = BusinessError("profile_not_found", "User profile not found.");
                await txService.FailAsync(begin.TransactionId, "profile_not_found", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            var alreadyUsed = await db.UserHints
                .AsNoTracking()
                .AnyAsync(x => x.UserId == userId && x.QuestionId == request.QuestionId && x.HintType == hintType, ct);
            if (alreadyUsed)
            {
                var response = new HintUseResponse(
                    Success: true,
                    AlreadyProcessed: false,
                    Coins: profile.Coins,
                    FreeHints: await GetFreeHintsRemainingAsync(db, userId, ct),
                    SpentCoins: 0,
                    UsedFreeHint: false,
                    ErrorCode: null,
                    Message: "Hint already unlocked.");
                await txService.CompleteAsync(begin.TransactionId, response, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Ok(response);
            }

            var freeHintsBefore = await GetFreeHintsRemainingAsync(db, userId, ct);
            var usedFreeHint = freeHintsBefore > 0;
            var spentCoins = 0;
            if (!usedFreeHint)
            {
                spentCoins = serverCost;
                if (profile.Coins < spentCoins)
                {
                    var error = BusinessError("insufficient_balance", "Insufficient coin balance.");
                    await txService.FailAsync(begin.TransactionId, "insufficient_balance", error, ct);
                    if (dbTx is not null) await dbTx.CommitAsync(ct);
                    return Results.Conflict(error);
                }

                profile.Coins -= spentCoins;
                profile.TotalCoinsSpent += spentCoins;
                profile.UpdatedAt = DateTime.UtcNow;
            }

            db.UserHints.Add(new UserHint
            {
                UserId = userId,
                QuestionId = request.QuestionId,
                HintType = hintType,
                UsedAt = DateTime.UtcNow
            });

            var responseAfterUse = new HintUseResponse(
                Success: true,
                AlreadyProcessed: false,
                Coins: profile.Coins,
                FreeHints: Math.Max(0, freeHintsBefore - (usedFreeHint ? 1 : 0)),
                SpentCoins: spentCoins,
                UsedFreeHint: usedFreeHint,
                ErrorCode: null,
                Message: null);

            await txService.CompleteAsync(begin.TransactionId, responseAfterUse, ct);
            if (dbTx is not null) await dbTx.CommitAsync(ct);
            return Results.Ok(responseAfterUse);
        });

        economy.MapPost("/rewards/claim", async (
            RewardClaimRequest request,
            ApiDbContext db,
            IEconomyTransactionService txService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = EndpointUser.GetUserId(ctx);
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            if (!ValidateIdempotencyKey(request.IdempotencyKey, out var keyError))
                return keyError!;
            if (string.IsNullOrWhiteSpace(request.RewardId))
                return Results.BadRequest(BusinessError("invalid_reward_id", "RewardId is required."));
            if (request.Coins < 0 || request.Xp < 0 || (request.Coins == 0 && request.Xp == 0))
                return Results.BadRequest(BusinessError("invalid_reward_payload", "Reward coins/xp must be non-negative and not both zero."));

            var normalizedRewardId = request.RewardId.Trim();
            var beginTuple = await TryBeginAsync(
                txService,
                userId,
                "economy_reward_claim",
                request.IdempotencyKey!,
                request with { RewardId = normalizedRewardId, RewardType = Normalize(request.RewardType) },
                ct);
            if (beginTuple.Error is not null)
                return beginTuple.Error;
            var begin = beginTuple.Begin!;
            var idempotencyResult = HandleIdempotentDecision(begin);
            if (idempotencyResult is not null)
                return idempotencyResult;

            await using var dbTx = await BeginDbTransactionIfSupportedAsync(db, ct);
            var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
            if (profile is null)
            {
                var error = BusinessError("profile_not_found", "User profile not found.");
                await txService.FailAsync(begin.TransactionId, "profile_not_found", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            var rewardState = await db.UserRewardStates.FirstOrDefaultAsync(
                x => x.UserId == userId && x.RewardKey == normalizedRewardId,
                ct);

            if (rewardState is not null && rewardState.Claimed)
            {
                var alreadyClaimed = new RewardClaimResponse(
                    Success: true,
                    AlreadyClaimed: true,
                    Coins: profile.Coins,
                    Xp: profile.Xp,
                    Reward: new RewardGrantSummary(0, 0),
                    ErrorCode: null,
                    Message: null);
                await txService.CompleteAsync(begin.TransactionId, alreadyClaimed, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Ok(alreadyClaimed);
            }

            if (rewardState is not null && !rewardState.Eligible)
            {
                var error = BusinessError("not_eligible", "Reward is not eligible.");
                await txService.FailAsync(begin.TransactionId, "not_eligible", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            if (rewardState is null)
            {
                rewardState = new UserRewardState
                {
                    UserId = userId,
                    RewardKey = normalizedRewardId,
                    Eligible = true,
                    Claimed = false,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                db.UserRewardStates.Add(rewardState);
            }

            profile.Coins += request.Coins;
            profile.TotalCoinsEarned += request.Coins;
            profile.Xp += request.Xp;
            profile.Level = 1 + (profile.Xp / 100);
            profile.UpdatedAt = DateTime.UtcNow;

            rewardState.Claimed = true;
            rewardState.ClaimedAtUtc = DateTime.UtcNow;
            rewardState.UpdatedAtUtc = DateTime.UtcNow;

            var response = new RewardClaimResponse(
                Success: true,
                AlreadyClaimed: false,
                Coins: profile.Coins,
                Xp: profile.Xp,
                Reward: new RewardGrantSummary(request.Coins, request.Xp),
                ErrorCode: null,
                Message: null);

            await txService.CompleteAsync(begin.TransactionId, response, ct);
            if (dbTx is not null) await dbTx.CommitAsync(ct);
            return Results.Ok(response);
        });

        var shop = app.MapGroup("/api/shop")
            .RequireAuthorization()
            .WithTags("Shop");

        shop.MapPost("/streak-freeze/purchase", async (
            StreakFreezePurchaseRequest request,
            ApiDbContext db,
            IEconomyTransactionService txService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = EndpointUser.GetUserId(ctx);
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            if (!ValidateIdempotencyKey(request.IdempotencyKey, out var keyError))
                return keyError!;
            if (request.Quantity <= 0)
                return Results.BadRequest(BusinessError("invalid_quantity", "Quantity must be greater than zero."));

            var beginTuple = await TryBeginAsync(
                txService,
                userId,
                "shop_streak_freeze_purchase",
                request.IdempotencyKey!,
                request,
                ct);
            if (beginTuple.Error is not null)
                return beginTuple.Error;
            var begin = beginTuple.Begin!;
            var idempotencyResult = HandleIdempotentDecision(begin);
            if (idempotencyResult is not null)
                return idempotencyResult;

            await using var dbTx = await BeginDbTransactionIfSupportedAsync(db, ct);
            var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
            if (profile is null)
            {
                var error = BusinessError("profile_not_found", "User profile not found.");
                await txService.FailAsync(begin.TransactionId, "profile_not_found", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            if (profile.StreakFreezeCount + request.Quantity > MaxStreakFreezes)
            {
                var error = BusinessError("max_inventory_reached", "Max streak freeze inventory reached.");
                await txService.FailAsync(begin.TransactionId, "max_inventory_reached", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            var spentCoins = request.Quantity * StreakFreezeUnitCost;
            if (profile.Coins < spentCoins)
            {
                var error = BusinessError("insufficient_balance", "Insufficient coin balance.");
                await txService.FailAsync(begin.TransactionId, "insufficient_balance", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            profile.Coins -= spentCoins;
            profile.TotalCoinsSpent += spentCoins;
            profile.StreakFreezeCount += request.Quantity;
            profile.UpdatedAt = DateTime.UtcNow;

            var response = new StreakFreezePurchaseResponse(
                Success: true,
                AlreadyProcessed: false,
                Coins: profile.Coins,
                StreakFreezeCount: profile.StreakFreezeCount,
                SpentCoins: spentCoins,
                ErrorCode: null,
                Message: null);

            await txService.CompleteAsync(begin.TransactionId, response, ct);
            if (dbTx is not null) await dbTx.CommitAsync(ct);
            return Results.Ok(response);
        });

        var seasons = app.MapGroup("/api/seasons")
            .RequireAuthorization()
            .WithTags("Seasons");

        seasons.MapPost("/daily-run-claim", async (
            SeasonDailyRunClaimRequest request,
            ApiDbContext db,
            IEconomyTransactionService txService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = EndpointUser.GetUserId(ctx);
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            if (!ValidateIdempotencyKey(request.IdempotencyKey, out var keyError))
                return keyError!;
            if (string.IsNullOrWhiteSpace(request.TransactionId))
                return Results.BadRequest(BusinessError("invalid_transaction_id", "TransactionId is required."));

            var beginTuple = await TryBeginAsync(
                txService,
                userId,
                "season_daily_run_claim",
                request.IdempotencyKey!,
                request,
                ct);
            if (beginTuple.Error is not null)
                return beginTuple.Error;
            var begin = beginTuple.Begin!;
            var idempotencyResult = HandleIdempotentDecision(begin);
            if (idempotencyResult is not null)
                return idempotencyResult;

            await using var dbTx = await BeginDbTransactionIfSupportedAsync(db, ct);
            var season = await ResolveActiveSeasonAsync(db, request.SeasonId, ct);
            if (season is null)
            {
                var error = BusinessError("invalid_season", "Season is missing or inactive.");
                await txService.FailAsync(begin.TransactionId, "invalid_season", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            var normalizedTxId = request.TransactionId.Trim();
            var existing = await db.UserSeasonDailyRunClaims
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.DailyRunTransactionId == normalizedTxId, ct);
            if (existing is not null)
            {
                var seasonStateExisting = await BuildSeasonStateAsync(db, userId, season.Id, ct);
                var replay = new SeasonDailyRunClaimResponse(
                    Success: true,
                    AlreadyClaimed: true,
                    AwardedXp: 0,
                    Season: seasonStateExisting,
                    ErrorCode: null,
                    Message: null);
                await txService.CompleteAsync(begin.TransactionId, replay, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Ok(replay);
            }

            var dailyRunClaim = await db.DailyRunChestClaims
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.TransactionId == normalizedTxId, ct);
            if (dailyRunClaim is null)
            {
                var error = BusinessError("not_eligible", "Daily Run claim transaction was not found.");
                await txService.FailAsync(begin.TransactionId, "not_eligible", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            var awardedXp = dailyRunClaim.Xp;
            var progress = await GetOrCreateSeasonProgressAsync(db, userId, season.Id, ct);
            progress.EarnedXp += awardedXp;
            progress.Level = 1 + (progress.EarnedXp / 100);
            progress.UpdatedAtUtc = DateTime.UtcNow;

            db.UserSeasonDailyRunClaims.Add(new UserSeasonDailyRunClaim
            {
                UserId = userId,
                SeasonId = season.Id,
                DailyRunTransactionId = normalizedTxId,
                DailyRunClaimId = dailyRunClaim.Id,
                AwardedXp = awardedXp,
                CreatedAtUtc = DateTime.UtcNow
            });

            var seasonState = await BuildSeasonStateAsync(db, userId, season.Id, ct);
            var response = new SeasonDailyRunClaimResponse(
                Success: true,
                AlreadyClaimed: false,
                AwardedXp: awardedXp,
                Season: seasonState,
                ErrorCode: null,
                Message: null);

            await txService.CompleteAsync(begin.TransactionId, response, ct);
            if (dbTx is not null) await dbTx.CommitAsync(ct);
            return Results.Ok(response);
        });

        seasons.MapPost("/milestones/{milestoneId:int}/claim", async (
            int milestoneId,
            SeasonMilestoneClaimRequest request,
            ApiDbContext db,
            IEconomyTransactionService txService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = EndpointUser.GetUserId(ctx);
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            if (!ValidateIdempotencyKey(request.IdempotencyKey, out var keyError))
                return keyError!;
            if (milestoneId <= 0)
                return Results.BadRequest(BusinessError("invalid_milestone", "MilestoneId must be greater than zero."));

            var beginTuple = await TryBeginAsync(
                txService,
                userId,
                "season_milestone_claim",
                request.IdempotencyKey!,
                new { request.SeasonId, milestoneId },
                ct);
            if (beginTuple.Error is not null)
                return beginTuple.Error;
            var begin = beginTuple.Begin!;
            var idempotencyResult = HandleIdempotentDecision(begin);
            if (idempotencyResult is not null)
                return idempotencyResult;

            await using var dbTx = await BeginDbTransactionIfSupportedAsync(db, ct);
            var season = await ResolveActiveSeasonAsync(db, request.SeasonId, ct);
            if (season is null)
            {
                var error = BusinessError("invalid_season", "Season is missing or inactive.");
                await txService.FailAsync(begin.TransactionId, "invalid_season", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            var milestone = await db.SeasonRewardTrackEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == milestoneId && x.SeasonId == season.Id && x.IsActive, ct);
            if (milestone is null)
            {
                var error = BusinessError("invalid_milestone", "Milestone was not found for this season.");
                await txService.FailAsync(begin.TransactionId, "invalid_milestone", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            var already = await db.UserSeasonMilestoneClaims
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.SeasonId == season.Id && x.MilestoneId == milestoneId, ct);
            if (already is not null)
            {
                var seasonStateExisting = await BuildSeasonStateAsync(db, userId, season.Id, ct);
                var rewardExisting = new SeasonMilestoneRewardResponse(
                    Type: already.RewardType,
                    Coins: already.CoinsAwarded ?? 0,
                    Xp: already.XpAwarded ?? 0,
                    CosmeticItemId: already.CosmeticItemId,
                    FragmentName: already.FragmentName,
                    FragmentCopies: already.FragmentCopiesAwarded);
                var replay = new SeasonMilestoneClaimResponse(
                    Success: true,
                    AlreadyClaimed: true,
                    AlreadyOwned: already.AlreadyOwned,
                    Season: seasonStateExisting,
                    Reward: rewardExisting,
                    ErrorCode: null,
                    Message: null);
                await txService.CompleteAsync(begin.TransactionId, replay, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Ok(replay);
            }

            var progress = await GetOrCreateSeasonProgressAsync(db, userId, season.Id, ct);
            if (progress.EarnedXp < milestone.XpRequired)
            {
                var error = BusinessError("insufficient_season_xp", "User has not reached the milestone XP threshold.");
                await txService.FailAsync(begin.TransactionId, "insufficient_season_xp", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
            if (profile is null)
            {
                var error = BusinessError("profile_not_found", "User profile not found.");
                await txService.FailAsync(begin.TransactionId, "profile_not_found", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            var rewardType = Normalize(milestone.RewardType);
            var reward = new SeasonMilestoneRewardResponse(rewardType, 0, 0, null, null, null);
            var alreadyOwned = false;

            if (rewardType == "coins")
            {
                if (!TryReadIntPayload(milestone.RewardPayloadJson, "coins", out var coins) || coins <= 0)
                {
                    var error = BusinessError("invalid_reward_payload", "Milestone reward payload is invalid.");
                    await txService.FailAsync(begin.TransactionId, "invalid_reward_payload", error, ct);
                    if (dbTx is not null) await dbTx.CommitAsync(ct);
                    return Results.Conflict(error);
                }

                profile.Coins += coins;
                profile.TotalCoinsEarned += coins;
                reward = reward with { Coins = coins };
            }
            else if (rewardType == "xp")
            {
                if (!TryReadIntPayload(milestone.RewardPayloadJson, "xp", out var xp) || xp <= 0)
                {
                    var error = BusinessError("invalid_reward_payload", "Milestone reward payload is invalid.");
                    await txService.FailAsync(begin.TransactionId, "invalid_reward_payload", error, ct);
                    if (dbTx is not null) await dbTx.CommitAsync(ct);
                    return Results.Conflict(error);
                }

                profile.Xp += xp;
                profile.Level = 1 + (profile.Xp / 100);
                reward = reward with { Xp = xp };
            }
            else if (rewardType is "cosmetic_item" or "cosmetic")
            {
                if (!TryReadIntPayload(milestone.RewardPayloadJson, "cosmeticItemId", out var itemId) || itemId <= 0)
                {
                    var error = BusinessError("invalid_reward_payload", "Milestone reward payload is invalid.");
                    await txService.FailAsync(begin.TransactionId, "invalid_reward_payload", error, ct);
                    if (dbTx is not null) await dbTx.CommitAsync(ct);
                    return Results.Conflict(error);
                }

                alreadyOwned = await db.UserCosmeticInventories
                    .AsNoTracking()
                    .AnyAsync(x => x.UserId == userId && x.CosmeticItemId == itemId && !x.IsRevoked, ct);
                if (!alreadyOwned)
                {
                    var item = await db.CosmeticItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == itemId, ct);
                    if (item is null)
                    {
                        var error = BusinessError("invalid_reward_payload", "Milestone cosmetic item was not found.");
                        await txService.FailAsync(begin.TransactionId, "invalid_reward_payload", error, ct);
                        if (dbTx is not null) await dbTx.CommitAsync(ct);
                        return Results.Conflict(error);
                    }

                    db.UserCosmeticInventories.Add(new UserCosmeticInventory
                    {
                        UserId = userId,
                        CosmeticItemId = itemId,
                        Source = "season",
                        SourceRef = $"season:{season.Id}:milestone:{milestoneId}",
                        GrantReason = "Season milestone reward",
                        SeasonId = season.Id,
                        AssetVersion = item.AssetVersion,
                        UnlockedAt = DateTime.UtcNow
                    });
                }

                reward = reward with { CosmeticItemId = itemId };
            }
            else if (rewardType == "cosmetic_fragment")
            {
                if (!TryReadStringPayload(milestone.RewardPayloadJson, "fragmentName", out var fragmentName) ||
                    !TryReadIntPayload(milestone.RewardPayloadJson, "copies", out var copies) ||
                    copies <= 0)
                {
                    var error = BusinessError("invalid_reward_payload", "Milestone fragment payload is invalid.");
                    await txService.FailAsync(begin.TransactionId, "invalid_reward_payload", error, ct);
                    if (dbTx is not null) await dbTx.CommitAsync(ct);
                    return Results.Conflict(error);
                }

                var fragmentProgress = await db.UserCosmeticFragmentProgresses
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.FragmentName == fragmentName, ct);
                if (fragmentProgress is null)
                {
                    fragmentProgress = new UserCosmeticFragmentProgress
                    {
                        UserId = userId,
                        FragmentName = fragmentName,
                        Copies = 0
                    };
                    db.UserCosmeticFragmentProgresses.Add(fragmentProgress);
                }

                fragmentProgress.Copies += copies;
                fragmentProgress.UpdatedAtUtc = DateTime.UtcNow;
                reward = reward with { FragmentName = fragmentName, FragmentCopies = copies };
            }
            else
            {
                var error = BusinessError("unsupported_reward_type", $"Unsupported milestone reward type '{rewardType}'.");
                await txService.FailAsync(begin.TransactionId, "unsupported_reward_type", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            profile.UpdatedAt = DateTime.UtcNow;
            db.UserSeasonMilestoneClaims.Add(new UserSeasonMilestoneClaim
            {
                UserId = userId,
                SeasonId = season.Id,
                MilestoneId = milestoneId,
                RewardType = reward.Type,
                CoinsAwarded = reward.Coins,
                XpAwarded = reward.Xp,
                CosmeticItemId = reward.CosmeticItemId,
                FragmentName = reward.FragmentName,
                FragmentCopiesAwarded = reward.FragmentCopies,
                AlreadyOwned = alreadyOwned,
                ClaimedAtUtc = DateTime.UtcNow
            });

            var seasonState = await BuildSeasonStateAsync(db, userId, season.Id, ct);
            var response = new SeasonMilestoneClaimResponse(
                Success: true,
                AlreadyClaimed: false,
                AlreadyOwned: alreadyOwned,
                Season: seasonState,
                Reward: reward,
                ErrorCode: null,
                Message: null);

            await txService.CompleteAsync(begin.TransactionId, response, ct);
            if (dbTx is not null) await dbTx.CommitAsync(ct);
            return Results.Ok(response);
        });

        var cosmetics = app.MapGroup("/api/cosmetics")
            .RequireAuthorization()
            .WithTags("Cosmetics");

        cosmetics.MapPost("/items/{itemId:int}/claim", async (
            int itemId,
            CosmeticItemClaimRequest request,
            ApiDbContext db,
            IEconomyTransactionService txService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = EndpointUser.GetUserId(ctx);
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            if (!ValidateIdempotencyKey(request.IdempotencyKey, out var keyError))
                return keyError!;
            if (itemId <= 0)
                return Results.BadRequest(BusinessError("invalid_item_id", "ItemId must be greater than zero."));

            var source = Normalize(request.Source);
            var beginTuple = await TryBeginAsync(
                txService,
                userId,
                "cosmetics_item_claim",
                request.IdempotencyKey!,
                new { itemId, source, request.Metadata },
                ct);
            if (beginTuple.Error is not null)
                return beginTuple.Error;
            var begin = beginTuple.Begin!;
            var idempotencyResult = HandleIdempotentDecision(begin);
            if (idempotencyResult is not null)
                return idempotencyResult;

            await using var dbTx = await BeginDbTransactionIfSupportedAsync(db, ct);
            var item = await db.CosmeticItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == itemId, ct);
            if (item is null || !item.IsActive)
            {
                var error = BusinessError("invalid_item", "Cosmetic item was not found or is inactive.");
                await txService.FailAsync(begin.TransactionId, "invalid_item", error, ct);
                if (dbTx is not null) await dbTx.CommitAsync(ct);
                return Results.Conflict(error);
            }

            var alreadyOwned = await db.UserCosmeticInventories
                .AsNoTracking()
                .AnyAsync(x => x.UserId == userId && x.CosmeticItemId == itemId && !x.IsRevoked, ct);
            if (!alreadyOwned)
            {
                db.UserCosmeticInventories.Add(new UserCosmeticInventory
                {
                    UserId = userId,
                    CosmeticItemId = itemId,
                    Source = string.IsNullOrWhiteSpace(source) ? "reward" : source,
                    SourceRef = $"{source}:{request.IdempotencyKey}",
                    GrantReason = "Cosmetic claim endpoint",
                    SeasonId = item.SeasonId,
                    AssetVersion = item.AssetVersion,
                    UnlockedAt = DateTime.UtcNow
                });
            }

            var response = new CosmeticItemClaimResponse(
                Success: true,
                AlreadyOwned: alreadyOwned,
                Inventory: await LoadInventoryItemIdsAsync(db, userId, ct),
                FragmentProgress: await LoadFragmentProgressAsync(db, userId, ct),
                ErrorCode: null,
                Message: null);

            await txService.CompleteAsync(begin.TransactionId, response, ct);
            if (dbTx is not null) await dbTx.CommitAsync(ct);
            return Results.Ok(response);
        });

        cosmetics.MapPost("/fragments/grant", async (
            CosmeticFragmentGrantRequest request,
            ApiDbContext db,
            IEconomyTransactionService txService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = EndpointUser.GetUserId(ctx);
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            if (!ValidateIdempotencyKey(request.IdempotencyKey, out var keyError))
                return keyError!;
            if (string.IsNullOrWhiteSpace(request.FragmentName))
                return Results.BadRequest(BusinessError("invalid_fragment", "FragmentName is required."));
            if (request.Copies <= 0)
                return Results.BadRequest(BusinessError("invalid_copies", "Copies must be greater than zero."));

            var source = Normalize(request.Source);
            var beginTuple = await TryBeginAsync(
                txService,
                userId,
                "cosmetics_fragment_grant",
                request.IdempotencyKey!,
                request with { Source = source },
                ct);
            if (beginTuple.Error is not null)
                return beginTuple.Error;
            var begin = beginTuple.Begin!;
            var idempotencyResult = HandleIdempotentDecision(begin);
            if (idempotencyResult is not null)
                return idempotencyResult;

            await using var dbTx = await BeginDbTransactionIfSupportedAsync(db, ct);
            var fragmentName = request.FragmentName!.Trim();
            var progress = await db.UserCosmeticFragmentProgresses
                .FirstOrDefaultAsync(x => x.UserId == userId && x.FragmentName == fragmentName, ct);
            if (progress is null)
            {
                progress = new UserCosmeticFragmentProgress
                {
                    UserId = userId,
                    FragmentName = fragmentName,
                    Copies = 0
                };
                db.UserCosmeticFragmentProgresses.Add(progress);
            }

            progress.Copies += request.Copies;
            progress.UpdatedAtUtc = DateTime.UtcNow;

            var itemUnlocked = false;
            int? unlockedItemId = null;
            if (FragmentUnlocks.TryGetValue(fragmentName, out var unlock))
            {
                var unlockItem = await db.CosmeticItems.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Key == unlock.ItemKey && x.IsActive, ct);
                if (unlockItem is not null && progress.Copies >= unlock.RequiredCopies)
                {
                    var alreadyOwned = await db.UserCosmeticInventories
                        .AsNoTracking()
                        .AnyAsync(x => x.UserId == userId && x.CosmeticItemId == unlockItem.Id && !x.IsRevoked, ct);
                    if (!alreadyOwned)
                    {
                        db.UserCosmeticInventories.Add(new UserCosmeticInventory
                        {
                            UserId = userId,
                            CosmeticItemId = unlockItem.Id,
                            Source = "fragment_unlock",
                            SourceRef = $"fragment:{fragmentName}",
                            GrantReason = $"Unlocked via {fragmentName}",
                            SeasonId = unlockItem.SeasonId,
                            AssetVersion = unlockItem.AssetVersion,
                            UnlockedAt = DateTime.UtcNow
                        });
                        itemUnlocked = true;
                        unlockedItemId = unlockItem.Id;
                    }
                }
            }

            var response = new CosmeticFragmentGrantResponse(
                Success: true,
                AlreadyProcessed: false,
                ItemUnlocked: itemUnlocked,
                UnlockedItemId: unlockedItemId,
                Inventory: await LoadInventoryItemIdsAsync(db, userId, ct),
                FragmentProgress: await LoadFragmentProgressAsync(db, userId, ct),
                ErrorCode: null,
                Message: null);

            await txService.CompleteAsync(begin.TransactionId, response, ct);
            if (dbTx is not null) await dbTx.CommitAsync(ct);
            return Results.Ok(response);
        });
    }

    private static bool ValidateIdempotencyKey(string? key, out IResult? error)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            error = Results.BadRequest(BusinessError("invalid_idempotency_key", "IdempotencyKey is required."));
            return false;
        }

        error = null;
        return true;
    }

    private static IResult? HandleIdempotentDecision(EconomyTransactionBeginResult begin)
    {
        if (begin.ShouldProcess)
            return null;

        if (begin.IsCompleted)
            return ReplayStoredJson(begin.ResultJson, successStatusCode: StatusCodes.Status200OK);

        if (begin.IsFailed)
            return ReplayStoredJson(begin.ResultJson, successStatusCode: MapErrorStatusCode(begin.ErrorCode));

        if (begin.IsPending)
            return Results.Conflict(BusinessError("transaction_in_progress", "A matching request is already being processed."));

        return Results.Conflict(BusinessError("idempotency_conflict", "Invalid transaction state."));
    }

    private static async Task<(EconomyTransactionBeginResult? Begin, IResult? Error)> TryBeginAsync(
        IEconomyTransactionService txService,
        string userId,
        string transactionType,
        string idempotencyKey,
        object requestPayload,
        CancellationToken ct)
    {
        try
        {
            var begin = await txService.BeginOrGetExistingAsync(userId, transactionType, idempotencyKey, requestPayload, ct);
            return (begin, null);
        }
        catch (EconomyTransactionConflictException)
        {
            return (null, Results.Conflict(BusinessError("idempotency_conflict", "Idempotency key already exists with a different payload.")));
        }
    }

    private static IResult ReplayStoredJson(string? resultJson, int successStatusCode)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return Results.StatusCode(successStatusCode);

        try
        {
            var node = JsonNode.Parse(resultJson);
            if (node is JsonObject obj)
            {
                if (obj.ContainsKey("alreadyProcessed"))
                    obj["alreadyProcessed"] = true;
                if (obj.ContainsKey("alreadyClaimed"))
                    obj["alreadyClaimed"] = true;
            }

            return Results.Json(node, statusCode: successStatusCode);
        }
        catch
        {
            return Results.Content(resultJson, "application/json", statusCode: successStatusCode);
        }
    }

    private static int MapErrorStatusCode(string? errorCode)
    {
        return errorCode switch
        {
            "insufficient_balance" => StatusCodes.Status409Conflict,
            "not_eligible" => StatusCodes.Status409Conflict,
            "invalid_season" => StatusCodes.Status409Conflict,
            "inactive_season" => StatusCodes.Status409Conflict,
            "invalid_reward_payload" => StatusCodes.Status409Conflict,
            "profile_not_found" => StatusCodes.Status409Conflict,
            "unsupported_reward_type" => StatusCodes.Status409Conflict,
            "invalid_item" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
    }

    private static ApiErrorResponse BusinessError(string errorCode, string message)
        => new(false, errorCode, message);

    private static async Task<int> GetFreeHintsRemainingAsync(ApiDbContext db, string userId, CancellationToken ct)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);
        var usedToday = await db.UserHints
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && x.UsedAt >= todayUtc && x.UsedAt < tomorrowUtc, ct);

        return Math.Max(0, DailyFreeHintLimit - usedToday);
    }

    private static async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginDbTransactionIfSupportedAsync(ApiDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsRelational())
            return null;

        return await db.Database.BeginTransactionAsync(ct);
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static async Task<CosmeticSeason?> ResolveActiveSeasonAsync(ApiDbContext db, int? requestedSeasonId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (requestedSeasonId.HasValue)
        {
            return await db.CosmeticSeasons
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == requestedSeasonId.Value &&
                    x.IsActive &&
                    x.StartDate <= now &&
                    x.EndDate >= now,
                    ct);
        }

        return await db.CosmeticSeasons
            .AsNoTracking()
            .Where(x => x.IsActive && x.StartDate <= now && x.EndDate >= now)
            .OrderByDescending(x => x.StartDate)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<UserSeasonProgress> GetOrCreateSeasonProgressAsync(ApiDbContext db, string userId, int seasonId, CancellationToken ct)
    {
        var progress = await db.UserSeasonProgresses
            .FirstOrDefaultAsync(x => x.UserId == userId && x.SeasonId == seasonId, ct);
        if (progress is not null)
            return progress;

        progress = new UserSeasonProgress
        {
            UserId = userId,
            SeasonId = seasonId,
            EarnedXp = 0,
            Level = 1,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.UserSeasonProgresses.Add(progress);
        return progress;
    }

    private static async Task<SeasonStateResponse> BuildSeasonStateAsync(ApiDbContext db, string userId, int seasonId, CancellationToken ct)
    {
        var progress = await db.UserSeasonProgresses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.SeasonId == seasonId, ct);

        var claimedIds = await db.UserSeasonMilestoneClaims
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.SeasonId == seasonId)
            .Select(x => x.MilestoneId)
            .OrderBy(x => x)
            .ToListAsync(ct);

        return new SeasonStateResponse(
            SeasonId: seasonId,
            EarnedXp: progress?.EarnedXp ?? 0,
            Level: progress?.Level ?? 1,
            ClaimedMilestoneIds: claimedIds);
    }

    private static bool TryReadIntPayload(string payloadJson, string propertyName, out int value)
    {
        value = 0;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            return doc.RootElement.TryGetProperty(propertyName, out var prop) && prop.TryGetInt32(out value);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadStringPayload(string payloadJson, string propertyName, out string value)
    {
        value = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty(propertyName, out var prop))
                return false;
            var text = prop.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return false;
            value = text.Trim();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<IReadOnlyList<int>> LoadInventoryItemIdsAsync(ApiDbContext db, string userId, CancellationToken ct)
    {
        return await db.UserCosmeticInventories
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsRevoked)
            .OrderBy(x => x.CosmeticItemId)
            .Select(x => x.CosmeticItemId)
            .ToListAsync(ct);
    }

    private static async Task<IReadOnlyDictionary<string, int>> LoadFragmentProgressAsync(ApiDbContext db, string userId, CancellationToken ct)
    {
        return await db.UserCosmeticFragmentProgresses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.FragmentName)
            .ToDictionaryAsync(x => x.FragmentName, x => x.Copies, ct);
    }
}

public sealed record ApiErrorResponse(
    bool Success,
    string ErrorCode,
    string Message
);

public sealed record CoinSpendRequest(
    string? IdempotencyKey,
    int Amount,
    string? Reason,
    JsonObject? Metadata
);

public sealed record CoinSpendResponse(
    bool Success,
    bool AlreadyProcessed,
    int Coins,
    int FreeHints,
    int SpentCoins,
    string? ErrorCode,
    string? Message
);

public sealed record HintUseRequest(
    string? IdempotencyKey,
    int QuestionId,
    string? HintType,
    int CostCoins
);

public sealed record HintUseResponse(
    bool Success,
    bool AlreadyProcessed,
    int Coins,
    int FreeHints,
    int SpentCoins,
    bool UsedFreeHint,
    string? ErrorCode,
    string? Message
);

public sealed record RewardClaimRequest(
    string? IdempotencyKey,
    string? RewardId,
    string? RewardType,
    int Coins,
    int Xp,
    JsonObject? Metadata
);

public sealed record RewardGrantSummary(
    int Coins,
    int Xp
);

public sealed record RewardClaimResponse(
    bool Success,
    bool AlreadyClaimed,
    int Coins,
    int Xp,
    RewardGrantSummary Reward,
    string? ErrorCode,
    string? Message
);

public sealed record StreakFreezePurchaseRequest(
    string? IdempotencyKey,
    int Quantity
);

public sealed record StreakFreezePurchaseResponse(
    bool Success,
    bool AlreadyProcessed,
    int Coins,
    int StreakFreezeCount,
    int SpentCoins,
    string? ErrorCode,
    string? Message
);

public sealed record SeasonDailyRunClaimRequest(
    string? IdempotencyKey,
    string? TransactionId,
    int? SeasonId,
    int? Xp
);

public sealed record SeasonStateResponse(
    int SeasonId,
    int EarnedXp,
    int Level,
    IReadOnlyList<int> ClaimedMilestoneIds
);

public sealed record SeasonDailyRunClaimResponse(
    bool Success,
    bool AlreadyClaimed,
    int AwardedXp,
    SeasonStateResponse Season,
    string? ErrorCode,
    string? Message
);

public sealed record SeasonMilestoneClaimRequest(
    string? IdempotencyKey,
    int? SeasonId
);

public sealed record SeasonMilestoneRewardResponse(
    string Type,
    int Coins,
    int Xp,
    int? CosmeticItemId,
    string? FragmentName,
    int? FragmentCopies
);

public sealed record SeasonMilestoneClaimResponse(
    bool Success,
    bool AlreadyClaimed,
    bool AlreadyOwned,
    SeasonStateResponse Season,
    SeasonMilestoneRewardResponse Reward,
    string? ErrorCode,
    string? Message
);

public sealed record CosmeticItemClaimRequest(
    string? IdempotencyKey,
    string? Source,
    JsonObject? Metadata
);

public sealed record CosmeticItemClaimResponse(
    bool Success,
    bool AlreadyOwned,
    IReadOnlyList<int> Inventory,
    IReadOnlyDictionary<string, int> FragmentProgress,
    string? ErrorCode,
    string? Message
);

public sealed record CosmeticFragmentGrantRequest(
    string? IdempotencyKey,
    string? FragmentName,
    int Copies,
    string? Source,
    JsonObject? Metadata
);

public sealed record CosmeticFragmentGrantResponse(
    bool Success,
    bool AlreadyProcessed,
    bool ItemUnlocked,
    int? UnlockedItemId,
    IReadOnlyList<int> Inventory,
    IReadOnlyDictionary<string, int> FragmentProgress,
    string? ErrorCode,
    string? Message
);

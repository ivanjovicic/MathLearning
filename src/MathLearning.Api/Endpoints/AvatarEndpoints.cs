using FluentValidation;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;

namespace MathLearning.Api.Endpoints;

public static class AvatarEndpoints
{
    public static void MapAvatarEndpoints(this IEndpointRouteBuilder app)
    {
        // Legacy cosmetics/avatar compatibility surface.
        // Keep read aliases that existing clients/tests depend on, but do not add new canonical mobile
        // settlement behavior here when /api/cosmetics/* or /api/economy/* already owns the contract.
        var group = app.MapGroup("/api/cosmetics")
            .RequireAuthorization()
            .WithTags("Cosmetics & Avatar");

        group.MapGet("/items", async (
            ICosmeticCatalogService catalogService,
            HttpContext ctx,
            CancellationToken cancellationToken,
            string? category,
            string? rarity,
            int? seasonId) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await catalogService.GetCatalogAsync(userId, category, rarity, seasonId, cancellationToken));
        });

        group.MapPost("/equip", async (
            EquipCosmeticRequest request,
            ICosmeticInventoryService inventoryService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await inventoryService.EquipSlotAsync(userId, request, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/equip-batch", async (
            EquipCosmeticBatchRequest request,
            ICosmeticInventoryService inventoryService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await inventoryService.EquipBatchAsync(userId, request, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/unequip", async (
            EquipCosmeticRequest request,
            ICosmeticInventoryService inventoryService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await inventoryService.EquipSlotAsync(userId, request with { CosmeticItemId = null }, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/purchase", async (
            PurchaseCosmeticRequest request,
            ApiDbContext db,
            ICosmeticsIdempotencyService idempotencyService,
            ICosmeticInventoryService inventoryService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            if (!CosmeticsEndpointHelpers.TryResolveMutationKeys(
                    request.OperationId,
                    request.IdempotencyKey,
                    request.TransactionId,
                    out var operationId,
                    out var idempotencyKey,
                    out var keyError))
            {
                return keyError!;
            }

            var beginTuple = await CosmeticsEndpointHelpers.TryBeginCosmeticsMutationAsync(
                idempotencyService,
                userId,
                "cosmetics_shop_purchase",
                operationId,
                idempotencyKey,
                new
                {
                    operationId,
                    idempotencyKey,
                    request.CosmeticItemId
                },
                cancellationToken);
            if (beginTuple.Error is not null)
            {
                return beginTuple.Error;
            }

            var begin = beginTuple.Begin!;
            var idempotencyResult = CosmeticsEndpointHelpers.HandleCosmeticsIdempotentDecision(begin, markAlreadyClaimed: false);
            if (idempotencyResult is not null)
            {
                return idempotencyResult;
            }

            await using var dbTx = await EconomyEndpointHelpers.BeginDbTransactionIfSupportedAsync(db, cancellationToken);
            try
            {
                var response = await inventoryService.PurchaseAsync(
                    userId,
                    request with { OperationId = operationId, IdempotencyKey = idempotencyKey },
                    cancellationToken);
                await idempotencyService.CompleteAsync(begin.LedgerId, response, cancellationToken);
                if (dbTx is not null) await dbTx.CommitAsync(cancellationToken);
                return Results.Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                var errorCode = ex.Message switch
                {
                    "Cosmetic item not found." => "invalid_item",
                    "This item cannot be purchased with coins." => "not_purchasable",
                    "This item is not available for purchase." => "not_purchasable",
                    "This item is not yet released." => "not_purchasable",
                    "This item is retired." => "not_purchasable",
                    "Item already owned." => "already_owned",
                    "Profile not found." => "profile_not_found",
                    "Insufficient coins." => "insufficient_balance",
                    _ => "purchase_failed"
                };
                var error = EconomyEndpointHelpers.BusinessError(errorCode, ex.Message);
                await idempotencyService.FailAsync(begin.LedgerId, errorCode, error, cancellationToken);
                if (dbTx is not null) await dbTx.CommitAsync(cancellationToken);
                return Results.Conflict(error);
            }
        });

        group.MapGet("/seasons", async (
            ICosmeticCatalogService catalogService,
            bool activeOnly,
            CancellationToken cancellationToken) =>
            Results.Ok(await catalogService.GetSeasonsAsync(activeOnly, cancellationToken)))
            .AllowAnonymous();

        group.MapGet("/reward-track", async (
            ICosmeticCatalogService catalogService,
            HttpContext ctx,
            CancellationToken cancellationToken,
            int? seasonId,
            string? trackType) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var response = await catalogService.GetRewardTrackAsync(userId, seasonId, trackType ?? "free", cancellationToken);
            return response is null ? Results.NotFound(new { error = "Reward track not found." }) : Results.Ok(response);
        });

        group.MapPost("/reward-track/claim", async (
            ClaimRewardTrackTierRequest request,
            ICosmeticRewardService rewardService,
            IValidator<ClaimRewardTrackTierRequest> validator,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            try
            {
                return Results.Ok(await rewardService.ClaimRewardTrackTierAsync(userId, request, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/avatar/{userId}", async (
            string userId,
            ICosmeticInventoryService inventoryService,
            CancellationToken cancellationToken) =>
            Results.Ok(await inventoryService.GetPublicAppearanceAsync(userId, cancellationToken)))
            .AllowAnonymous();

        app.MapGet("/api/avatar/me", async (
            ICosmeticInventoryService inventoryService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await inventoryService.GetAvatarAsync(userId, cancellationToken));
        }).RequireAuthorization();

        app.MapGet("/api/profile/{userId}/appearance", async (
            string userId,
            ICosmeticInventoryService inventoryService,
            CancellationToken cancellationToken) =>
            Results.Ok(await inventoryService.GetPublicAppearanceAsync(userId, cancellationToken)))
            .AllowAnonymous();
    }
}

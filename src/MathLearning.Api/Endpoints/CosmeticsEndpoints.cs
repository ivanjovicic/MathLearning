using System.Text.Json.Nodes;

using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Application.Services;

using MathLearning.Application.Services;

using MathLearning.Domain.Entities;

using MathLearning.Infrastructure.Persistance;

using MathLearning.Infrastructure.Services.Cosmetics;

using Microsoft.EntityFrameworkCore;



namespace MathLearning.Api.Endpoints;



public static class CosmeticsEndpoints

{

    public static void MapCosmeticsEndpoints(this IEndpointRouteBuilder app)

    {

        var cosmetics = app.MapGroup("/api/cosmetics")

            .RequireAuthorization()

            .WithTags("Cosmetics");



        cosmetics.MapGet("/catalog", async (

            IMobileCosmeticsService mobileCosmeticsService,

            HttpContext ctx,

            CancellationToken ct,

            string? category,

            string? rarity,

            int? seasonId) =>

        {

            var userId = EndpointUser.GetUserId(ctx);

            if (string.IsNullOrWhiteSpace(userId))

                return Results.Unauthorized();



            var catalog = await mobileCosmeticsService.GetPublishedCatalogAsync(category, rarity, seasonId, ct);

            var etag = $"\"{catalog.CatalogVersion}\"";

            if (ctx.Request.Headers.IfNoneMatch.Any(value => string.Equals(value, etag, StringComparison.Ordinal)))

                return Results.StatusCode(StatusCodes.Status304NotModified);



            ctx.Response.Headers.ETag = etag;

            ctx.Response.Headers.CacheControl = "private, max-age=300";

            return Results.Ok(catalog);

        })

        .WithName("GetMobileCosmeticsCatalog")

        .WithSummary("Published cosmetic metadata list");



        cosmetics.MapGet("/inventory", async (

            IMobileCosmeticsService mobileCosmeticsService,

            HttpContext ctx,

            CancellationToken ct) =>

        {

            var userId = EndpointUser.GetUserId(ctx);

            if (string.IsNullOrWhiteSpace(userId))

                return Results.Unauthorized();



            return Results.Ok(await mobileCosmeticsService.GetMobileInventoryAsync(userId, ct));

        })

        .WithName("GetMobileCosmeticsInventory")

        .WithSummary("Current user's unlocked cosmetic keys and fragment progress");



        cosmetics.MapGet("/avatar", async (

            IMobileCosmeticsService mobileCosmeticsService,

            HttpContext ctx,

            CancellationToken ct) =>

        {

            var userId = EndpointUser.GetUserId(ctx);

            if (string.IsNullOrWhiteSpace(userId))

                return Results.Unauthorized();



            return Results.Ok(await mobileCosmeticsService.GetMobileAvatarAsync(userId, ct));

        })

        .WithName("GetMobileCosmeticsAvatar")

        .WithSummary("Current user's equipped avatar slots");



        cosmetics.MapPut("/avatar", async (

            MobileCosmeticAvatarUpdateRequest request,

            IMobileCosmeticsService mobileCosmeticsService,

            HttpContext ctx,

            CancellationToken ct) =>

        {

            var userId = EndpointUser.GetUserId(ctx);

            if (string.IsNullOrWhiteSpace(userId))

                return Results.Unauthorized();



            if (request.Slots is null || request.Slots.Count == 0)

                return Results.BadRequest(new { error = "At least one avatar slot must be provided." });



            try

            {

                return Results.Ok(await mobileCosmeticsService.UpdateMobileAvatarAsync(userId, request, ct));

            }

            catch (CosmeticAvatarOwnershipException ex)

            {

                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);

            }

            catch (InvalidOperationException ex)

            {

                return Results.BadRequest(new { error = ex.Message });

            }

        })

        .WithName("UpdateMobileCosmeticsAvatar")

        .WithSummary("Persist equipped avatar slots with ownership validation");



        cosmetics.MapPost("/items/{itemKey}/claim", async (

            string itemKey,

            CosmeticItemClaimRequest request,

            ApiDbContext db,

            ICosmeticEntitlementService entitlementService,

            ICosmeticsIdempotencyService idempotencyService,

            HttpContext ctx,

            CancellationToken ct) =>

        {

            var userId = EndpointUser.GetUserId(ctx);

            if (string.IsNullOrWhiteSpace(userId))

                return Results.Unauthorized();



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



            if (string.IsNullOrWhiteSpace(itemKey))

                return Results.BadRequest(EconomyEndpointHelpers.BusinessError("invalid_item_key", "ItemKey is required."));

            var normalizedItemKey = itemKey.Trim();

            if (!request.EntitlementId.HasValue || request.EntitlementId == Guid.Empty)

                return Results.Conflict(EconomyEndpointHelpers.BusinessError("not_eligible", "A server-issued cosmetic entitlement is required."));

            var entitlement = await entitlementService.GetEntitlementAsync(userId, request.EntitlementId.Value, ct);

            if (entitlement is null || !string.Equals(entitlement.EntitlementType, CosmeticEntitlementTypes.Item, StringComparison.OrdinalIgnoreCase))

                return Results.Conflict(EconomyEndpointHelpers.BusinessError("not_eligible", "Cosmetic entitlement was not found for this user."));

            if (!string.Equals(entitlement.ItemKey, normalizedItemKey, StringComparison.OrdinalIgnoreCase))

                return Results.Conflict(EconomyEndpointHelpers.BusinessError("entitlement_mismatch", "Entitlement does not match the requested cosmetic item."));

            var beginTuple = await CosmeticsEndpointHelpers.TryBeginCosmeticsMutationAsync(

                idempotencyService,

                userId,

                "cosmetics_item_claim",

                operationId,

                idempotencyKey,

                new

                {

                    operationId,

                    idempotencyKey,

                    entitlementId = entitlement.Id,

                    itemKey = normalizedItemKey,

                    sourceType = entitlement.SourceType,

                    sourceRef = entitlement.SourceRef

                },

                ct);

            if (beginTuple.Error is not null)

                return beginTuple.Error;

            var begin = beginTuple.Begin!;

            var idempotencyResult = CosmeticsEndpointHelpers.HandleCosmeticsIdempotentDecision(begin, markAlreadyClaimed: true);

            if (idempotencyResult is not null)

                return idempotencyResult;



            await using var dbTx = await EconomyEndpointHelpers.BeginDbTransactionIfSupportedAsync(db, ct);

            var item = await db.CosmeticItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entitlement.CosmeticItemId, ct);

            if (item is null || !item.IsActive)

            {

                var error = EconomyEndpointHelpers.BusinessError("invalid_item", "Cosmetic item was not found or is inactive.");

                await idempotencyService.FailAsync(begin.LedgerId, "invalid_item", error, ct);

                if (dbTx is not null) await dbTx.CommitAsync(ct);

                return Results.Conflict(error);

            }



            var alreadyOwned = await db.UserCosmeticInventories

                .AsNoTracking()

                .AnyAsync(x => x.UserId == userId && x.CosmeticItemId == item.Id && !x.IsRevoked, ct);

            if (!alreadyOwned)

            {

                var consumed = await entitlementService.TryConsumeEntitlementAsync(
                    userId,
                    entitlement.Id,
                    "cosmetics_item_claim",
                    operationId,
                    idempotencyKey,
                    ct);

                if (!consumed)

                {

                    var error = EconomyEndpointHelpers.BusinessError("not_eligible", "Cosmetic entitlement has already been consumed.");

                    await idempotencyService.FailAsync(begin.LedgerId, "not_eligible", error, ct);

                    if (dbTx is not null) await dbTx.CommitAsync(ct);

                    return Results.Conflict(error);

                }

                db.UserCosmeticInventories.Add(new UserCosmeticInventory

                {

                    UserId = userId,

                    CosmeticItemId = item.Id,

                    Source = entitlement.SourceType,

                    SourceRef = entitlement.SourceRef,

                    GrantReason = "Cosmetic entitlement claim",

                    SeasonId = item.SeasonId,

                    AssetVersion = item.AssetVersion,

                    UnlockedAt = DateTime.UtcNow

                });

            }



            await db.SaveChangesAsync(ct);



            var response = new CosmeticItemClaimResponse(

                Success: true,

                AlreadyClaimed: alreadyOwned,

                AlreadyProcessed: false,

                Conflict: false,

                ItemKey: normalizedItemKey,

                Inventory: await LoadInventoryItemKeysAsync(db, userId, ct),

                FragmentProgress: await CosmeticsFragmentService.LoadFragmentProgressByLabelAsync(db, userId, ct),

                ErrorCode: null,

                Message: null);



            await idempotencyService.CompleteAsync(begin.LedgerId, response, ct);

            if (dbTx is not null) await dbTx.CommitAsync(ct);

            return Results.Ok(response);

        })

        .WithName("ClaimCosmeticItem")

        .WithSummary("Grant full cosmetic item ownership");



        cosmetics.MapPost("/fragments/grant", async (

            CosmeticFragmentGrantRequest request,

            ApiDbContext db,

            ICosmeticEntitlementService entitlementService,

            ICosmeticsIdempotencyService idempotencyService,

            ICosmeticsFragmentService fragmentService,

            HttpContext ctx,

            CancellationToken ct) =>

        {

            var userId = EndpointUser.GetUserId(ctx);

            if (string.IsNullOrWhiteSpace(userId))

                return Results.Unauthorized();



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



            var source = CosmeticsEndpointHelpers.ResolveSource(request.Source, request.SourceType);
            var transactionId = DailyRunCosmeticsSettlement.ResolveTransactionId(request.TransactionId, operationId);
            string fragmentName;
            int copies;
            CosmeticEntitlementDto? entitlement = null;

            if (DailyRunCosmeticsSettlement.IsDailyRunSource(source))
            {
                var settlement = await DailyRunCosmeticsSettlement.ResolveFragmentGrantAsync(
                    db, userId, transactionId!, ct);
                if (settlement.Error is not null)
                    return settlement.Error;

                fragmentName = settlement.FragmentName;
                copies = settlement.Copies;
            }
            else
            {
                if (!request.EntitlementId.HasValue || request.EntitlementId == Guid.Empty)
                    return Results.Conflict(EconomyEndpointHelpers.BusinessError("not_eligible", "A server-issued cosmetic entitlement is required."));

                entitlement = await entitlementService.GetEntitlementAsync(userId, request.EntitlementId.Value, ct);
                if (entitlement is null || !string.Equals(entitlement.EntitlementType, CosmeticEntitlementTypes.Fragment, StringComparison.OrdinalIgnoreCase))
                    return Results.Conflict(EconomyEndpointHelpers.BusinessError("not_eligible", "Cosmetic entitlement was not found for this user."));

                fragmentName = entitlement.FragmentLabel ?? entitlement.ItemKey;
                copies = entitlement.Quantity;

                if (!string.IsNullOrWhiteSpace(request.FragmentName) &&
                    !string.Equals(request.FragmentName.Trim(), fragmentName, StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Conflict(EconomyEndpointHelpers.BusinessError("entitlement_mismatch", "Entitlement does not match the requested fragment."));
                }

                if (request.Copies > 0 && request.Copies != copies)
                {
                    return Results.Conflict(EconomyEndpointHelpers.BusinessError("entitlement_mismatch", "Entitlement does not match the requested fragment quantity."));
                }
            }

            var beginTuple = await CosmeticsEndpointHelpers.TryBeginCosmeticsMutationAsync(

                idempotencyService,

                userId,

                "cosmetics_fragment_grant",

                operationId,

                idempotencyKey,

                new

                {

                    operationId,

                    idempotencyKey,

                    entitlementId = entitlement?.Id,

                    transactionId,

                    fragmentName,

                    copies,

                    sourceType = entitlement?.SourceType ?? source,

                    sourceRef = entitlement?.SourceRef ?? transactionId

                },

                ct);

            if (beginTuple.Error is not null)

                return beginTuple.Error;

            var begin = beginTuple.Begin!;

            var idempotencyResult = CosmeticsEndpointHelpers.HandleCosmeticsIdempotentDecision(begin, markAlreadyClaimed: false);

            if (idempotencyResult is not null)

                return idempotencyResult;



            var target = await fragmentService.ResolveFragmentTargetAsync(fragmentName, ct);

            if (target is null)

            {

                return Results.BadRequest(EconomyEndpointHelpers.BusinessError("invalid_fragment", "Fragment is not recognized."));

            }



            await using var dbTx = await EconomyEndpointHelpers.BeginDbTransactionIfSupportedAsync(db, ct);

            if (entitlement is not null)
            {
                var consumed = await entitlementService.TryConsumeEntitlementAsync(
                    userId,
                    entitlement.Id,
                    "cosmetics_fragment_grant",
                    operationId,
                    idempotencyKey,
                    ct);
                if (!consumed)
                {
                    var error = EconomyEndpointHelpers.BusinessError("not_eligible", "Cosmetic entitlement has already been consumed.");
                    await idempotencyService.FailAsync(begin.LedgerId, "not_eligible", error, ct);
                    if (dbTx is not null) await dbTx.CommitAsync(ct);
                    return Results.Conflict(error);
                }
            }

            var nowUtc = DateTime.UtcNow;

            FragmentGrantResult grantResult;

            try

            {

                grantResult = await fragmentService.GrantFragmentsAsync(userId, fragmentName, copies, nowUtc, ct);

            }

            catch (InvalidOperationException ex)

            {

                var error = EconomyEndpointHelpers.BusinessError("invalid_fragment", ex.Message);

                await idempotencyService.FailAsync(begin.LedgerId, "invalid_fragment", error, ct);

                if (dbTx is not null) await dbTx.CommitAsync(ct);

                return Results.BadRequest(error);

            }



            await db.SaveChangesAsync(ct);



            CosmeticInventoryGrantDto? unlockedInventory = null;

            if (grantResult.UnlockedAt.HasValue && grantResult.UnlockedItemKey is not null)

            {

                unlockedInventory = new CosmeticInventoryGrantDto(

                    grantResult.UnlockedItemKey,

                    grantResult.UnlockedAt.Value,

                    grantResult.UnlockedSource ?? "fragment_unlock");

            }



            var progressDto = new CosmeticFragmentProgressDto(

                grantResult.UnlockedItemKey ?? target.ItemKey,

                grantResult.Collected,

                grantResult.Required,

                grantResult.UpdatedAtUtc,

                grantResult.ProgressUnlockedAtUtc ?? grantResult.UnlockedAt);



            var response = new CosmeticFragmentGrantResponse(

                Success: true,

                AlreadyProcessed: false,

                Conflict: false,

                ItemUnlocked: grantResult.ItemUnlocked,

                UnlockedItemId: grantResult.UnlockedItemKey,

                UnlockedInventory: unlockedInventory,

                Progress: progressDto,

                Inventory: await LoadInventoryItemKeysAsync(db, userId, ct),

                FragmentProgress: await CosmeticsFragmentService.LoadFragmentProgressByLabelAsync(db, userId, ct),

                ErrorCode: null,

                Message: null);



            await idempotencyService.CompleteAsync(begin.LedgerId, response, ct);

            if (dbTx is not null) await dbTx.CommitAsync(ct);

            return Results.Ok(response);

        })

        .WithName("GrantCosmeticFragment")

        .WithSummary("Increment fragment progress toward an item");

    }



    private static async Task<IReadOnlyList<string>> LoadInventoryItemKeysAsync(ApiDbContext db, string userId, CancellationToken ct)

    {

        return await db.UserCosmeticInventories

            .AsNoTracking()

            .Where(x => x.UserId == userId && !x.IsRevoked)

            .Join(

                db.CosmeticItems.AsNoTracking(),

                inventory => inventory.CosmeticItemId,

                item => item.Id,

                (_, item) => item.Key)

            .Distinct()

            .OrderBy(x => x)

            .ToListAsync(ct);

    }

}



public sealed record CosmeticItemClaimRequest(

    string? OperationId,

    string? IdempotencyKey,

    string? TransactionId,

    Guid? EntitlementId,

    string? Source,

    string? SourceType,

    string? SourceEvent,

    JsonObject? Metadata

);



public sealed record CosmeticItemClaimResponse(

    bool Success,

    bool AlreadyClaimed,

    bool AlreadyProcessed,

    bool Conflict,

    string ItemKey,

    IReadOnlyList<string> Inventory,

    IReadOnlyDictionary<string, int> FragmentProgress,

    string? ErrorCode,

    string? Message

);



public sealed record CosmeticFragmentGrantRequest(

    string? OperationId,

    string? IdempotencyKey,

    string? TransactionId,

    Guid? EntitlementId,

    string? FragmentName,

    int Copies,

    string? Source,

    string? SourceType,

    string? SourceEvent,

    JsonObject? Metadata

);



public sealed record CosmeticFragmentProgressDto(

    string ItemId,

    int CollectedFragments,

    int RequiredFragments,

    DateTime UpdatedAt,

    DateTime? UnlockedAt

);



public sealed record CosmeticInventoryGrantDto(

    string ItemKey,

    DateTime UnlockedAt,

    string Source

);



public sealed record CosmeticFragmentGrantResponse(

    bool Success,

    bool AlreadyProcessed,

    bool Conflict,

    bool ItemUnlocked,

    string? UnlockedItemId,

    CosmeticInventoryGrantDto? UnlockedInventory,

    CosmeticFragmentProgressDto Progress,

    IReadOnlyList<string> Inventory,

    IReadOnlyDictionary<string, int> FragmentProgress,

    string? ErrorCode,

    string? Message

);



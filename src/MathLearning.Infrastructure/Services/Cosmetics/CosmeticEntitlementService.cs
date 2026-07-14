using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services.Cosmetics;

public sealed class CosmeticEntitlementService : ICosmeticEntitlementService
{
    private readonly ApiDbContext db;

    public CosmeticEntitlementService(ApiDbContext db)
    {
        this.db = db;
    }

    public async Task<CosmeticEntitlementDto?> GetEntitlementAsync(
        string userId,
        Guid entitlementId,
        CancellationToken cancellationToken)
    {
        return await db.CosmeticEntitlements
            .AsNoTracking()
            .Where(x => x.Id == entitlementId && x.UserId == userId)
            .Join(
                db.CosmeticItems.AsNoTracking(),
                entitlement => entitlement.CosmeticItemId,
                item => item.Id,
                (entitlement, item) => ToDto(entitlement, item))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CosmeticEntitlementDto> CreateItemEntitlementAsync(
        string userId,
        int cosmeticItemId,
        string sourceType,
        string sourceRef,
        string operationKey,
        CancellationToken cancellationToken)
    {
        return await CreateAsync(
            userId,
            CosmeticEntitlementTypes.Item,
            cosmeticItemId,
            1,
            sourceType,
            sourceRef,
            operationKey,
            cancellationToken);
    }

    public async Task<CosmeticEntitlementDto> CreateFragmentEntitlementAsync(
        string userId,
        int cosmeticItemId,
        int quantity,
        string sourceType,
        string sourceRef,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Fragment entitlement quantity must be positive.");
        }

        return await CreateAsync(
            userId,
            CosmeticEntitlementTypes.Fragment,
            cosmeticItemId,
            quantity,
            sourceType,
            sourceRef,
            operationKey,
            cancellationToken);
    }

    public async Task<bool> TryConsumeEntitlementAsync(
        string userId,
        Guid entitlementId,
        string operationType,
        string operationId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var consumedAtUtc = DateTime.UtcNow;
        if (!db.Database.IsRelational())
        {
            var entitlement = await db.CosmeticEntitlements.FirstOrDefaultAsync(
                x => x.Id == entitlementId && x.UserId == userId,
                cancellationToken);
            if (entitlement is null || entitlement.ConsumedAtUtc.HasValue)
            {
                return false;
            }

            entitlement.ConsumedAtUtc = consumedAtUtc;
            entitlement.ConsumedOperationType = operationType;
            entitlement.ConsumedOperationId = operationId;
            entitlement.ConsumedIdempotencyKey = idempotencyKey;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        var affected = await db.CosmeticEntitlements
            .Where(x => x.Id == entitlementId && x.UserId == userId && x.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.ConsumedAtUtc, _ => consumedAtUtc)
                    .SetProperty(x => x.ConsumedOperationType, _ => operationType)
                    .SetProperty(x => x.ConsumedOperationId, _ => operationId)
                    .SetProperty(x => x.ConsumedIdempotencyKey, _ => idempotencyKey),
                cancellationToken);

        return affected == 1;
    }

    private async Task<CosmeticEntitlementDto> CreateAsync(
        string userId,
        string entitlementType,
        int cosmeticItemId,
        int quantity,
        string sourceType,
        string sourceRef,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var existing = await db.CosmeticEntitlements
            .Include(x => x.CosmeticItem)
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.OperationKey == operationKey,
                cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing, existing.CosmeticItem);
        }

        var item = await db.CosmeticItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cosmeticItemId, cancellationToken)
            ?? throw new InvalidOperationException("Cosmetic item was not found.");

        var entitlement = new CosmeticEntitlement
        {
            UserId = userId,
            EntitlementType = entitlementType,
            CosmeticItemId = cosmeticItemId,
            Quantity = quantity,
            SourceType = sourceType,
            SourceRef = sourceRef,
            OperationKey = operationKey,
            GrantedAtUtc = DateTime.UtcNow
        };

        db.CosmeticEntitlements.Add(entitlement);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entitlement, item);
    }

    private static CosmeticEntitlementDto ToDto(CosmeticEntitlement entitlement, CosmeticItem item)
        => new(
            entitlement.Id,
            entitlement.UserId,
            entitlement.EntitlementType,
            entitlement.CosmeticItemId,
            item.Key,
            item.FragmentLabel,
            entitlement.Quantity,
            entitlement.SourceType,
            entitlement.SourceRef,
            entitlement.GrantedAtUtc,
            entitlement.ConsumedAtUtc);
}

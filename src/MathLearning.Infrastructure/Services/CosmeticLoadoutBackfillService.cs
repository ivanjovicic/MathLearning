// Helper class for backfilling cosmetic loadout metadata during deployment
// Add to Infrastructure/Services folder if needed, or inline in migration/bootstrap logic

using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services;

/// <summary>
/// One-time backfill utility for populating UserCosmeticLoadoutProjection JSON metadata columns.
/// Run this after deploying migration AddCosmeticLoadoutMetadataColumns.
/// 
/// Usage in Hangfire or startup:
///   var backfill = new CosmeticLoadoutBackfillService(db, projectionService);
///   await backfill.BackfillAllAsync();
/// </summary>
public class CosmeticLoadoutBackfillService
{
    private readonly ApiDbContext _db;
    private readonly CosmeticLoadoutProjectionService _projectionService;
    private const int BatchSize = 1000;

    public CosmeticLoadoutBackfillService(
        ApiDbContext db,
        CosmeticLoadoutProjectionService projectionService)
    {
        _db = db;
        _projectionService = projectionService;
    }

    /// <summary>
    /// Backfills all projections that lack metadata.
    /// Queries rows where FrameJson is null (indicating old data),
    /// then rebuilds using the projection service.
    /// </summary>
    public async Task BackfillAllAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Find all projections that need backfill (FrameJson is null = no metadata yet)
        var userIds = await _db.UserCosmeticLoadoutProjections
            .Where(p => p.FrameJson == null)
            .Select(p => p.UserId)
            .ToListAsync();

        if (userIds.Count == 0)
        {
            Console.WriteLine("[Backfill] No projections to backfill. All up-to-date.");
            return;
        }

        Console.WriteLine($"[Backfill] Starting backfill for {userIds.Count} users...");

        var processed = 0;
        var errors = 0;

        // Process in batches
        foreach (var batch in userIds.Chunk(BatchSize))
        {
            try
            {
                await _projectionService.RebuildBatchAsync(batch);
                processed += batch.Length;
                Console.WriteLine($"[Backfill] Processed {processed}/{userIds.Count} users...");
            }
            catch (Exception ex)
            {
                errors++;
                Console.WriteLine($"[Backfill] Error processing batch: {ex.Message}");
            }
        }

        stopwatch.Stop();
        Console.WriteLine($"[Backfill] Completed in {stopwatch.ElapsedMilliseconds}ms. " +
            $"Processed: {processed}, Errors: {errors}");
    }

    /// <summary>
    /// Backfills a specific user (useful for admin commands or testing).
    /// </summary>
    public async Task BackfillUserAsync(string userId)
    {
        await _projectionService.RebuildForUserAsync(userId);
        Console.WriteLine($"[Backfill] User {userId} backfilled.");
    }

    /// <summary>
    /// Validates that backfill is complete by checking for any null metadata columns.
    /// </summary>
    public async Task<(int Total, int WithMetadata, int Missing)> ValidateAsync()
    {
        var total = await _db.UserCosmeticLoadoutProjections.CountAsync();
        var withMetadata = await _db.UserCosmeticLoadoutProjections
            .Where(p => p.FrameJson != null || p.TrailJson != null || 
                        p.AvatarGearJson != null || p.ProfileBackgroundJson != null)
            .CountAsync();

        var missing = total - withMetadata;
        return (total, withMetadata, missing);
    }
}

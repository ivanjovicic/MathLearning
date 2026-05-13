# Backend DTO Design: Cosmetic Display Metadata

**Date**: May 13, 2026  
**Scope**: Leaderboard, user profile, and any cosmetic loadout display  
**Goal**: Frontend should never need to guess cosmetic names, rarities, or unlock sources from IDs

---

## 1. DTO Architecture

### New DTOs

**`EquippedCosmeticDto`** — Rich metadata for a single equipped cosmetic
```csharp
public record EquippedCosmeticDto(
    int ItemId,
    string Key,
    string Name,
    string Category,
    string Rarity,
    string? UnlockSource,        // e.g., "daily_run_chest", "xp_milestone"
    string? AssetPath,
    string? PreviewAssetPath = null,
    bool IsDefault = false,
    string? CompatibilityInfo = null
);
```

**`CosmeticLoadoutDto`** — Complete loadout with metadata per slot
```csharp
public record CosmeticLoadoutDto(
    EquippedCosmeticDto? Frame = null,
    EquippedCosmeticDto? Trail = null,
    EquippedCosmeticDto? AvatarGear = null,
    EquippedCosmeticDto? AnswerEffect = null,
    EquippedCosmeticDto? ProfileBackground = null,
    IReadOnlyList<RareUnlockDto>? RecentRareUnlocks = null,
    long LoadoutVersion = 1,
    DateTime? LastUpdatedUtc = null
);
```

### Response DTOs (Updated)

**`UserProfileDto`** — Profile with loadout metadata
- **Added**: `CosmeticLoadoutDto? CosmeticLoadout` (new clients use this)
- **Retained**: ID fields (`AvatarFrameId`, `TrailId`, etc.) for backward compatibility (old clients ignore)

**`LeaderboardItemDto`** — Leaderboard entry with loadout metadata
- **Added**: `CosmeticLoadoutDto? CosmeticLoadout` (new clients use this)
- **Retained**: ID fields for backward compatibility (old clients ignore)

---

## 2. Data Model & Projection

### Updated Entity: `UserCosmeticLoadoutProjection`

```csharp
public class UserCosmeticLoadoutProjection
{
    public string UserId { get; set; }                    // PK
    
    // Backward compat: keep ID fields
    public int? AvatarFrameId { get; set; }
    public int? TrailId { get; set; }
    public int? AvatarGearId { get; set; }
    public int? AnswerEffectId { get; set; }
    public int? ProfileBackgroundId { get; set; }
    
    // Rich metadata: JSON columns (JSONB in PostgreSQL)
    public string? FrameJson { get; set; }               // Serialized EquippedCosmeticDto
    public string? TrailJson { get; set; }
    public string? AvatarGearJson { get; set; }
    public string? AnswerEffectJson { get; set; }
    public string? ProfileBackgroundJson { get; set; }
    
    // Rare unlocks history
    public string? RecentRareUnlocksJson { get; set; }   // List<RareUnlockDto>
    
    // Cache busting
    public long LoadoutVersion { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
```

### Database Schema (PostgreSQL)

```sql
CREATE TABLE user_cosmetic_loadout_projections (
    user_id VARCHAR(450) PRIMARY KEY,
    
    -- Backward compat IDs (indexed for rare direct queries)
    avatar_frame_id INT,
    trail_id INT,
    avatar_gear_id INT,
    answer_effect_id INT,
    profile_background_id INT,
    
    -- Rich metadata as JSONB (no joins needed for reads)
    frame_json JSONB,
    trail_json JSONB,
    avatar_gear_json JSONB,
    answer_effect_json JSONB,
    profile_background_json JSONB,
    
    recent_rare_unlocks_json JSONB,
    
    loadout_version BIGINT,
    updated_at_utc TIMESTAMP WITH TIME ZONE,
    
    -- Indexes
    INDEX IX_user_cosmetic_loadouts_frame_id (avatar_frame_id),
    INDEX IX_user_cosmetic_loadouts_bg_id (profile_background_id)
);
```

---

## 3. Query & Access Strategy

### Write Pattern (Infrequent)
When user equips a cosmetic or unlocks a new one:
1. `CosmeticLoadoutProjectionService.UpdateAfterEquipAsync(userId)` is called
2. Service loads:
   - `UserAvatarConfig` (which slots are equipped)
   - `UserCosmeticInventories` + **JOIN with `cosmetic_items`** (metadata)
3. Service builds `EquippedCosmeticDto` for each slot
4. Service serializes to JSON and stores in projection
5. **Join happens here, not in read path**

### Read Pattern (Frequent)
When displaying leaderboard or profile:
1. Query `UserCosmeticLoadoutProjection` (one row per user, no joins)
2. Deserialize JSON columns into `CosmeticLoadoutDto`
3. Return in response DTO
4. **Zero joins, no `cosmetic_items` table access**

### Deferred Consistency
- Projection is updated **after** equip action completes
- In the rare case of race condition (user equips, then read before projection updates), response includes ID fields (backward compatible)
- Client can fall back to local cosmetic catalog if needed (graceful degradation)

---

## 4. Migration & Backfill Strategy

### Step 1: Schema Migration
Migration: `AddCosmeticLoadoutMetadataColumns`

Adds 5 new JSONB columns to `user_cosmetic_loadout_projections`:
- `frame_json`
- `trail_json`
- `avatar_gear_json`
- `answer_effect_json`
- `profile_background_json`

**No data loss**: Existing ID columns remain intact, new columns nullable.

### Step 2: Backfill Existing Data
After deploying migration, run background job:
```csharp
public async Task BackfillCosmeticLoadoutMetadataAsync()
{
    var projections = await _db.UserCosmeticLoadoutProjections
        .Where(p => p.FrameJson == null)  // Find old rows
        .Select(p => p.UserId)
        .ToListAsync();
    
    // Rebuild in batches of 1000
    foreach (var batch in projections.Chunk(1000))
    {
        await _cosmeticLoadoutService.RebuildBatchAsync(batch);
    }
}
```

**Timing**: Run overnight or low-traffic period. Existing reads unaffected (ID fields still available).

### Step 3: Migration Removal (Optional, 30 days later)
Once 100% of data has metadata, drop ID columns if desired. **Recommended**: Keep for 90 days as fallback.

---

## 5. Service Implementation

### `CosmeticLoadoutProjectionService`

**`RebuildBatchAsync(userIds)`** — Rebuilds projection for a batch of users
```csharp
// Loads avatar configs and inventory + cosmetic_items
// Builds EquippedCosmeticDto for each slot with full metadata
// Serializes to JSON and stores in projection
// Called on: equip, unlock, admin grant
// Performance: O(n) users, 1-2 DB queries per batch
```

**`DeserializeLoadout(projection)`** — Converts projection to response DTO
```csharp
// Deserializes JSON columns into CosmeticLoadoutDto
// Used in endpoint/service layers
// Performance: In-memory JSON deserial, ~1-5ms per user
```

**`GetLoadoutsAsync(userIds)`** — Batch load projections
```csharp
// No joins, just SELECT from projection table
// Performance: Index scan, ~10ms for 100 users
```

---

## 6. Frontend Compatibility

### Phase 1: Dual-Mode (Current)
Frontend receives both old and new data:
```json
{
  "userProfileDto": {
    "avatarFrameId": 42,              // Old clients use this
    "avatarUrl": "...",
    "cosmeticLoadout": {              // New clients use this
      "frame": {
        "itemId": 42,
        "key": "frame_comet",
        "name": "Comet Frame",
        "rarity": "rare",
        "category": "frame",
        "unlockSource": "daily_run_chest",
        "assetPath": "/assets/cosmetics/frame_comet.png"
      },
      "recentRareUnlocks": [...]
    }
  }
}
```

### Client Migration Path
1. **Legacy clients**: Ignore `cosmeticLoadout`, use `avatarFrameId` + local catalog lookup
2. **Modern clients**: Prefer `cosmeticLoadout` object, use rich metadata for rendering
3. **Best practice**: Check if `cosmeticLoadout.frame` exists, else fall back to `avatarFrameId`

### Example Frontend Code
```typescript
// Modern client
const equipped = userProfile.cosmeticLoadout?.frame;
if (equipped) {
  // Render with rich metadata
  renderFrame(equipped.name, equipped.assetPath, equipped.rarity);
} else if (userProfile.avatarFrameId) {
  // Legacy fallback: local catalog lookup (not recommended)
  const item = COSMETIC_CATALOG[userProfile.avatarFrameId];
  renderFrame(item?.name || `Frame #${userProfile.avatarFrameId}`, ...);
}
```

---

## 7. Performance & Caching

### Read Performance
- **Leaderboard query** (100 users): 10-15ms (projection SELECT) + 5-20ms (JSON deserialization) = **15-35ms total**
- **Profile query** (1 user): 2-3ms

### Caching Strategy
1. **Response DTO caching**: Cache `UserProfileDto` in Redis for 5 minutes
2. **Projection caching**: In-memory cache of projection per user, invalidate on `UpdateAfterEquipAsync`
3. **JSONB at DB layer**: PostgreSQL's JSONB is faster than string JSONB

### Cache Invalidation
```csharp
// When user equips cosmetic:
await _cosmeticLoadoutService.UpdateAfterEquipAsync(userId);
await _cache.RemoveAsync($"profile:{userId}");
await _cache.RemoveAsync($"loadout:{userId}");
```

---

## 8. Handling Edge Cases

### Case 1: User has equipped cosmetic, but item is revoked
**Solution**: `RebuildBatchAsync` validates ownership before including in DTO.
Result: Slot becomes `null` in loadout.

### Case 2: User's cosmetic inventory is corrupted (item deleted from DB)
**Solution**: `BuildEquippedCosmeticDto` returns `null` if item not found.
Result: Slot is `null` (safe, not errors).

### Case 3: Leaderboard query spans 10,000 users
**Solution**: Projection query is indexed, batch load in chunks of 1000.
Result: 10 DB queries, all fast, no heavy joins.

### Case 4: New cosmetic category added (e.g., "pet")
**Solution**: Add new slot to `UserAvatarConfig`, new field in `CosmeticLoadoutDto`.
No breaking changes: Existing responses have fields as `null`.

---

## 9. Monitoring & Maintenance

### Metrics to Track
- `CosmeticLoadoutProjection.RebuildDuration` — Should stay < 500ms per 100 users
- `LoadoutJsonSize` — Average bytes per slot (should be < 500 bytes)
- `ProjectionUpdateFrequency` — How often projections are rebuilt (should be < 10/user/day)

### Alerts
- If JSON column size > 10KB: Investigate data corruption or runaway metadata
- If rebuild duration > 5s for 100 users: Query performance degradation
- If `RecentRareUnlocks` > 500 items: Rare unlock list growing beyond expectation

---

## 10. Testing Strategy

### Unit Tests
- `CosmeticLoadoutProjectionService.RebuildBatchAsync`: Verify DTO shape, JSON correctness
- `DeserializeLoadout`: Round-trip serialization
- Edge case: Revoked cosmetics, deleted items, null slots

### Integration Tests
- Equip cosmetic → Projection updated → Read returns rich metadata
- Leaderboard query with 100 users → All projections loaded and deserialized
- Backfill job → Old projections populated with metadata

### Load Tests
- Leaderboard query: 10,000 users, should stay < 100ms p99

---

## 11. Deployment Checklist

- [ ] Deploy migration `AddCosmeticLoadoutMetadataColumns`
- [ ] Verify schema: `\d user_cosmetic_loadout_projections` shows new columns
- [ ] Deploy code with updated service and DTOs
- [ ] Run backfill job (can be async overnight)
- [ ] Monitor logs for `RebuildBatchAsync` errors
- [ ] Enable metric collection on `LoadoutJsonSize`
- [ ] Release frontend with dual-mode support
- [ ] Gradual migration of clients to use `CosmeticLoadout` object
- [ ] (Optional, 90 days later) Drop ID columns, simplify schema

---

## Summary

| Aspect | Solution |
|--------|----------|
| **Frontend guessing** | Rich metadata in `CosmeticLoadoutDto` |
| **Backward compatibility** | Keep ID fields + new loadout object |
| **Heavy joins** | Denormalize to JSONB in projection at write time |
| **Cache safety** | Version field + explicit invalidation |
| **Edge cases** | Validation in projection rebuild, null slots for missing items |
| **Performance** | Leaderboard 100 users: 15-35ms (no joins) |
| **Migration** | Additive columns, backfill async, zero downtime |

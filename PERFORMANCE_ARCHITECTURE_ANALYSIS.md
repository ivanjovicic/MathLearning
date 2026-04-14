# MathLearning — Performance Architecture Analysis

**Date:** March 9, 2026  
**Goal:** Reduce database load by **10–30×** while keeping behavior identical.

---

## PART 1 — QUERY PATTERN ANALYSIS

### Critical Hot Paths (ranked by DB impact)

| # | Pattern | Queries/call | Calls/sec (10k users) | Est. DB queries/sec | Read/Write |
|---|---------|:---:|:---:|:---:|:---:|
| 1 | **School leaderboard refresh** (Hangfire every 10 min) | 16–20 + 4–8 writes | 0.0017 (continuous) | 0.03 (burst of ~20 per run) | 80/20 |
| 2 | **Quiz answer submission** (`POST /api/quiz/answer`) | 8–10 + cascaded XP | ~50 | **400–500** | 70/30 |
| 3 | **XP award cascade** (per answer when XP given) | 11–18 (incl. school aggregation) | ~50 | **550–900** | 60/40 |
| 4 | **Leaderboard view** (`GET /api/leaderboard`) | 7–12 | ~20 | **140–240** | 100/0 |
| 5 | **Global leaderboard** (`GET /api/leaderboard/global`) | 4 (full table scans) | ~5 | **20** (but each is O(n) rows) | 100/0 |
| 6 | **Cosmetic reward processing** | 3–10 (N+1) | ~50 (per answer) | **150–500** | 50/50 |
| 7 | **Offline sync submit** (10 answers batch) | 15–18 (N+1 idempotency) | ~2 | **30–36** | 70/30 |
| 8 | **Daily aggregation job** (2 AM) | 3N (N = active users) | burst | **30k+ for 10k users** | 80/20 |
| 9 | **Avatar/appearance load** | 1 | ~20 | **20** | 100/0 |
| 10 | **Offline bundle generation** | 6 | ~2 | **12** | 100/0 |

**Total estimated DB queries/sec at 10k concurrent users: ~1,300–2,200 Q/s**  
**Dominated by:** answer submission cascade (#2 + #3 + #6 combined = ~1,100–1,900 Q/s)

### Per-Pattern Breakdown

#### 1. Quiz Answer Submission (the single biggest cascade)

One `POST /api/quiz/answer` triggers:

```
Quiz answer recorded        → 3–4 queries (question, session, stats, save)
XpTrackingService.AddXp     → 2 queries (profile load, dupe check)
SchoolLeaderboard.ApplyXp   → 8–12 queries (4 period reads + rank recompute)
CosmeticRewardService       → 3–10 queries (N+1 per reward rule)
IngestService               → 2–3 queries (weakness tracking)
─────────────────────────────────────────
TOTAL                       → 18–31 DB round-trips per answer
```

At 10k users × 5 answers/min = 833 answers/sec → **15,000–25,800 queries/sec**.  
This is the primary bottleneck.

#### 2. Leaderboard Queries

`GetLeaderboardAsync()` executes:
1. `FindAsync(userId)` — PK lookup
2. Scoped query with `OrderByDescending(score)` + `Take(limit+1)` — index scan
3. `LoadAppearanceMapAsync()` — batch IN query
4. `ComputeRank()` — `CountAsync()` scanning all users with higher score (called twice)
5. `CountScope()` — another `CountAsync()` for percentile

**`ComputeRank`** is the worst part: `COUNT(*) WHERE score > myScore` requires scanning the full filtered scope. Called **twice** (first-rank and my-rank).

#### 3. Global Leaderboard (legacy)

Loads **entire UserProfiles table** into memory, then does LINQ-to-Objects sorting:
```csharp
var profileDict = await db.UserProfiles.Select(...).ToListAsync();    // ALL rows
var globalList = await (from s in db.UserQuestionStats group...).ToListAsync();  // ALL rows
var weeklyDict = await db.UserAnswers.Where(week).GroupBy(...).ToListAsync();   // week's answers
```
Memory usage = O(total_users). Response time = O(total_users). **Will crash at scale.**

#### 4. School Aggregation (per-XP cascade)

Every XP event triggers `ApplyXpChangeAsync()` which:
- Reloads or recomputes school metrics for **4 periods** (day/week/month/all_time)
- Each calls `GetOrRecomputeRowAsync()` → potential `GroupBy` on UserProfiles
- Then `RecomputeRanksForPeriodAsync()` loads **all schools** in period, recalculates ranks in-memory

#### 5. Cosmetic Rewards (N+1)

`ProcessRewardSourceAsync()`:
```csharp
foreach (var rule in rules)
{
    var item = await db.CosmeticItems.FirstOrDefaultAsync(...);  // 1 query per rule
    if (item != null) {
        // 2 AnyAsync calls to check dupes
        await TryGrantItemAsync(...);  // 2 more queries
    }
}
```
For 5 active reward rules → **15 queries per answer submission**.

---

## PART 2 — HOT TABLE DETECTION

### Table Heat Map

| Table | Read freq | Write freq | Rows (10k users) | Bottleneck reason |
|-------|:---------:|:----------:|:----------------:|:-----------------:|
| **UserProfiles** | 🔴 EXTREME | 🟠 HIGH | 10k | Every leaderboard query, every XP update, every login, school aggregation GROUP BY |
| **UserQuestionStats** | 🟠 HIGH | 🟠 HIGH | ~500k | Global leaderboard aggregation, quiz stats, weakness analysis |
| **UserAnswers** | 🟡 MEDIUM | 🟠 HIGH | ~5M | Weekly leaderboard calc, offline idempotency checks, append-heavy |
| **SchoolScoreAggregates** | 🟠 HIGH | 🟠 HIGH | ~200 (schools×periods) | Every school leaderboard view, refreshed every 10 min, updated per XP event |
| **UserXpEvents** | 🟡 MEDIUM | 🟠 HIGH | ~2M | Deduplication checks, audit trail, append-only |
| **CosmeticRewardClaims** | 🟡 MEDIUM | 🟡 MEDIUM | ~50k | Duplicate checks on every reward processing |
| **UserCosmeticInventory** | 🟡 MEDIUM | 🟡 LOW | ~30k | Catalog + inventory loads |
| **cosmetic_telemetry_events** | 🟢 LOW | 🟠 HIGH | ~1M+ | Append-only telemetry, unbounded growth |
| **cosmetic_audit_log** | 🟢 LOW | 🟡 MEDIUM | ~100k | Append-only audit trail, unbounded growth |
| **UserAppearanceProjection** | 🟠 HIGH | 🟡 LOW | 10k | Loaded per leaderboard view (batch) |

### Why These Tables Are Bottlenecks

**UserProfiles** — The single most contended table:
- Every leaderboard query reads it (sorted by XP columns)
- Every XP award writes to it (DailyXp, WeeklyXp, MonthlyXp, Xp, Level, Coins)
- School aggregation `GROUP BY SchoolId` scans the whole table 4× per refresh
- Reader-writer lock contention on hot rows

**UserQuestionStats** — Heavy aggregate reads:
- Global leaderboard does `GROUP BY UserId` with `SUM(CorrectAttempts)` on the entire table
- Per-user stats lookup on every question load
- Weakness analysis aggregates per topic

**SchoolScoreAggregates** — High update frequency:
- Updated on **every XP event** (not batched)
- Rank recomputation loads all rows per period
- 10-minute refresh job rebuilds from scratch

---

## PART 3 — CACHE STRATEGY

### Current State

| Layer | Technology | What's cached | TTL | Coverage |
|-------|-----------|---------------|-----|----------|
| L1 (Memory) | `IMemoryCache` (SizeLimit=100) | BKT params (6h), Explanations (12h), Design tokens, Adaptive recommendations (5min) | Varies | ~5% of read load |
| L2 (Redis) | StackExchange.Redis | Leaderboard sorted sets (if configured) | None | Leaderboard only |
| None | — | **UserProfiles, cosmetic catalog, school rankings, avatar configs** | — | **0% cached** |

### Proposed 3-Tier Cache Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  L0: Per-Request Cache (HttpContext.Items)                   │
│  Scope: single HTTP request        TTL: request lifetime    │
│  Content: UserProfile, UserSettings, current user lookups   │
│  Estimated hit rate: eliminates duplicate FindAsync calls    │
├─────────────────────────────────────────────────────────────┤
│  L1: In-Memory Cache (IMemoryCache — raise SizeLimit)       │
│  Scope: single server instance      TTL: 30s–5min           │
│  Content: see table below                                   │
│  Estimated hit rate: 85–95% for read-heavy data             │
├─────────────────────────────────────────────────────────────┤
│  L2: Redis Cache (IDistributedCache)                        │
│  Scope: all server instances         TTL: 1–10min           │
│  Content: leaderboard pages, school rankings, catalogs      │
│  Estimated hit rate: 90–99% for shared data                 │
└─────────────────────────────────────────────────────────────┘
```

### Cache Targets and TTLs

| Data | Cache Layer | TTL | Invalidation | Est. Query Reduction |
|------|:----------:|:---:|:------------:|:-------------------:|
| **Cosmetic item catalog** | L1 + L2 | 5 min | On admin change (event) | **99%** — catalog changes rarely |
| **Leaderboard page (top 50)** | L2 (Redis sorted set) | 30 sec | XP event increments score | **95%** — most views serve same data |
| **User avatar config** | L1 | 60 sec | On avatar save | **90%** — loaded per leaderboard entry |
| **User cosmetic inventory** | L1 | 60 sec | On purchase/grant | **90%** — loaded per catalog view |
| **School rankings** | L2 | 2 min | Hangfire refresh | **98%** — refreshed every 10 min anyway |
| **Reward rules** | L1 | 5 min | On admin change | **99%** — static configuration data |
| **UserProfile (own)** | L0 (request) | Request | — | **50%** — eliminates duplicate FindAsync |
| **Question catalog** | L1 | 10 min | On content change | **95%** — questions are semi-static |
| **Appearance projections** | L1 | 30 sec | On avatar change | **80%** — frequently batch-loaded |

### Expected Impact

At 10k users with current ~1,500 Q/s:
- Cosmetic catalog cache: −150 Q/s
- Leaderboard cache: −200 Q/s  
- Avatar/appearance cache: −60 Q/s
- School ranking cache: −50 Q/s
- Reward rules cache: −100 Q/s
- Request-scoped profile cache: −200 Q/s

**Total cache reduction: ~760 Q/s → ~50% of current load eliminated by caching alone.**

---

## PART 4 — READ MODEL PROJECTIONS

### Existing Projections (already in schema)

| Projection | Table | Status | Used by |
|-----------|-------|--------|---------|
| `user_appearance_projection` | ✅ Exists | Used for leaderboard appearance | `LoadAppearanceMapAsync()` |
| `SchoolScoreAggregates` | ✅ Exists | Pre-computed school rankings | School leaderboard endpoints |
| `SchoolRankHistories` | ✅ Exists | Historical snapshots | School history endpoint |

### Proposed New Projections

#### Projection 1: `leaderboard_snapshot`

**Purpose:** Eliminate full-table `UserProfiles` scan + `ComputeRank()` COUNT queries.

```
Table: leaderboard_snapshot
─────────────────────────────
user_id        VARCHAR(450) PK
scope          VARCHAR(32)      -- 'global', 'school:{id}', 'faculty:{id}'
period         VARCHAR(16)      -- 'daily', 'weekly', 'monthly', 'all_time'
rank           INT
score          INT
display_name   VARCHAR(200)
level          INT
streak         INT
updated_at     TIMESTAMPTZ
```

- **Populated by:** Background job (expand current 10-min refresh to include user rankings)
- **Indexed:** `(scope, period, rank)` for `ORDER BY rank LIMIT N` cursor pagination
- **Eliminates:** `ComputeRank()` full-table COUNT (2 per request), `CountScope()`, ad-hoc `ORDER BY DailyXp/WeeklyXp/MonthlyXp` on UserProfiles
- **Query reduction per leaderboard request: 7→2 queries (rank lookup + appearance batch)**

#### Projection 2: `user_quiz_summary`

**Purpose:** Replace the global leaderboard's full-table aggregation.

```
Table: user_quiz_summary
─────────────────────────────
user_id             VARCHAR(450) PK
total_correct       INT
total_attempts      INT
weekly_correct      INT
weekly_xp           INT
updated_at          TIMESTAMPTZ
```

- **Populated by:** Incremental update on each answer (instead of re-aggregating)
- **Event:** `POST /api/quiz/answer` → `UPDATE user_quiz_summary SET total_correct += 1`
- **Eliminates:** `GROUP BY UserId` on 500k-row UserQuestionStats + `GROUP BY UserId` on 5M-row UserAnswers
- **Global leaderboard: O(n) GROUP BY → O(1) indexed read with LIMIT**

#### Projection 3: `user_reward_state`

**Purpose:** Pre-compute reward eligibility, eliminate N+1 per-rule checks.

```
Table: user_reward_state
─────────────────────────────
user_id          VARCHAR(450)
reward_key       VARCHAR(128)
eligible         BOOLEAN
claimed          BOOLEAN
claimed_at       TIMESTAMPTZ
UNIQUE (user_id, reward_key)
```

- **Populated by:** Background job or event-driven after XP/level/streak changes
- **Eliminates:** Per-rule `FirstOrDefaultAsync` + per-item `AnyAsync` loops (N+1)
- **Reward check: N+1 loop → single WHERE user_id = @id batch read**

### Projection Impact Summary

| Projection | Queries eliminated per call | Calls/sec | Reduction |
|-----------|:--------------------------:|:---------:|:---------:|
| `leaderboard_snapshot` | 5 (2× ComputeRank + CountScope + profile sort + data load) | 20/s | **−100 Q/s** |
| `user_quiz_summary` | 3 (full aggregations → indexed read) | 5/s | **−15 Q/s** (but eliminates O(n) full scans) |
| `user_reward_state` | 5–10 (N+1 per reward rule) | 50/s | **−250–500 Q/s** |

---

## PART 5 — QUERY OPTIMIZATION

### Fix 1: Eliminate `ComputeRank()` Full-Table COUNT

**Current** (called 2× per leaderboard request):
```csharp
var higher = await query.CountAsync(u =>
    ScoreSelector.ScoreOf(u, period) > myScore ||
    (ScoreSelector.ScoreOf(u, period) == myScore && int.Parse(u.UserId) < myId));
```
Problem: Scans all rows matching the scope. `int.Parse(u.UserId)` forces client-side evaluation.

**Fix — Option A (leaderboard_snapshot):** Pre-computed rank in projection table. Single indexed lookup `WHERE user_id = @id`.

**Fix — Option B (window function):** If projection is not ready, use PostgreSQL `ROW_NUMBER()`:
```sql
SELECT rank FROM (
    SELECT "UserId", ROW_NUMBER() OVER (ORDER BY "DailyXp" DESC, "UserId") AS rank
    FROM "UserProfiles"
    WHERE "LeaderboardOptIn" = true
) sub WHERE "UserId" = @userId
```
Still O(n) but single query instead of 2× COUNT, and avoids `int.Parse` client-side.

### Fix 2: Rewrite Global Leaderboard with LIMIT in DB

**Current:**
```csharp
var profileDict = await db.UserProfiles.Select(...).ToListAsync();  // ALL rows
// + 2 more full aggregations, then LINQ sort in memory
```

**Rewrite:**
```csharp
var ranked = await db.UserProfiles
    .AsNoTracking()
    .Where(u => u.LeaderboardOptIn)
    .OrderByDescending(u => range == "weekly" ? u.WeeklyXp : u.Xp)
    .Take(limit)
    .Select(u => new { u.UserId, u.DisplayName, u.Username, u.Level, u.Xp, u.WeeklyXp, u.Streak })
    .ToListAsync();
```
Uses existing `(LeaderboardOptIn, Xp)` composite index. O(limit) instead of O(n).

### Fix 3: Batch Idempotency Checks in Offline Submit

**Current (N+1):**
```csharp
foreach (var answer in answers)
{
    var exists = await db.UserAnswers.AnyAsync(x => x.UserId == userId 
        && x.QuestionId == answer.QuestionId && x.AnsweredAt == answer.AnsweredAt);
}
```

**Rewrite (batch):**
```csharp
var keys = answers.Select(a => (a.QuestionId, a.AnsweredAt)).ToHashSet();
var existing = await db.UserAnswers.AsNoTracking()
    .Where(x => x.UserId == userId && keys.Select(k => k.QuestionId).Contains(x.QuestionId))
    .Select(x => new { x.QuestionId, x.AnsweredAt })
    .ToListAsync();
var existingSet = existing.ToHashSet();
// Then check existingSet in loop — 0 additional queries
```
**10 queries → 1 query**.

### Fix 4: Batch Cosmetic Reward Processing

**Current (N+1):**
```csharp
foreach (var rule in rules) {
    var item = await db.CosmeticItems.FirstOrDefaultAsync(x => x.Id == rule.CosmeticItemId);
    var alreadyClaimed = await db.CosmeticRewardClaims.AnyAsync(...);
    var alreadyOwned = await db.UserCosmeticInventories.AnyAsync(...);
}
```

**Rewrite (batch):**
```csharp
var itemIds = rules.Select(r => r.CosmeticItemId).Distinct().ToList();
var items = await db.CosmeticItems.AsNoTracking()
    .Where(x => itemIds.Contains(x.Id))
    .ToDictionaryAsync(x => x.Id);
var claimed = (await db.CosmeticRewardClaims.AsNoTracking()
    .Where(x => x.UserId == userId && itemIds.Contains(x.CosmeticItemId))
    .Select(x => x.CosmeticItemId)
    .ToListAsync()).ToHashSet();
var owned = (await db.UserCosmeticInventories.AsNoTracking()
    .Where(x => x.UserId == userId && itemIds.Contains(x.CosmeticItemId))
    .Select(x => x.CosmeticItemId)
    .ToListAsync()).ToHashSet();
// 3 batch queries replaces 3N queries
```
**15 queries → 3 queries**.

### Fix 5: Keyset Pagination Everywhere

Replace any OFFSET-based pagination with keyset cursor:
```csharp
// Instead of: .Skip(page * size).Take(size)
// Use:        .Where(x => x.Score < cursorScore || (x.Score == cursorScore && x.Id > cursorId))
//             .OrderByDescending(x => x.Score).ThenBy(x => x.Id).Take(size + 1)
```
Already used in main leaderboard (good). Ensure all paginated endpoints follow this.

---

## PART 6 — INDEX STRATEGY

### Existing Indexes (confirmed in ApiDbContext Fluent API)

| Table | Index | Columns | Status |
|-------|-------|---------|:------:|
| UserProfiles | Composite | (LeaderboardOptIn, Xp) | ✅ |
| UserProfiles | Composite | (LeaderboardOptIn, WeeklyXp) | ✅ |
| UserProfiles | Composite | (LeaderboardOptIn, MonthlyXp) | ✅ |
| UserProfiles | Composite | (LeaderboardOptIn, DailyXp) | ✅ |
| UserProfiles | Composite | (SchoolId, LeaderboardOptIn) | ✅ |
| UserProfiles | Composite | (FacultyId, LeaderboardOptIn) | ✅ |
| SchoolScoreAggregates | Composite | (Period, PeriodStartUtc, Rank) | ✅ |
| SchoolScoreAggregates | Composite | (Period, PeriodStartUtc, CompositeScore, SchoolId) | ✅ |
| SchoolRankHistories | Composite | (SchoolId, Period, PeriodStartUtc, SnapshotTimeUtc) | ✅ |

### Proposed New Indexes

#### High Priority (immediate impact)

```sql
-- 1. Covering index for leaderboard — avoids heap fetch for top-N queries
CREATE INDEX CONCURRENTLY IX_userprofiles_leaderboard_xp_covering
ON "UserProfiles" ("LeaderboardOptIn", "Xp" DESC, "UserId")
INCLUDE ("DisplayName", "Username", "Level", "Streak", "SchoolId")
WHERE "LeaderboardOptIn" = true;

-- 2. School aggregation — partial index for active school members
CREATE INDEX CONCURRENTLY IX_userprofiles_school_xp
ON "UserProfiles" ("SchoolId", "DailyXp", "WeeklyXp", "MonthlyXp", "Xp")
WHERE "LeaderboardOptIn" = true AND "SchoolId" IS NOT NULL;

-- 3. Offline idempotency — composite for batch existence checks
CREATE INDEX CONCURRENTLY IX_useranswers_idempotency
ON "UserAnswers" ("UserId", "QuestionId", "AnsweredAt");

-- 4. Reward claim deduplication — already has unique index, verify it covers:
-- UX_cosmetic_reward_claims_user_reward_source ON (UserId, RewardKey, SourceRef) — ✅ exists

-- 5. XP event deduplication
CREATE INDEX CONCURRENTLY IX_userxpevents_user_source
ON "UserXpEvents" ("UserId", "SourceType", "SourceId")
WHERE "SourceId" IS NOT NULL;
```

#### Medium Priority (query patterns that benefit)

```sql
-- 6. Weekly answer aggregation (for global leaderboard if kept)
CREATE INDEX CONCURRENTLY IX_useranswers_weekly_agg
ON "UserAnswers" ("UserId", "IsCorrect", "AnsweredAt" DESC)
WHERE "IsCorrect" = true;

-- 7. Telemetry event pruning
CREATE INDEX CONCURRENTLY IX_cosmetic_telemetry_prune
ON cosmetic_telemetry_events ("OccurredAtUtc");

-- 8. Audit log pruning
CREATE INDEX CONCURRENTLY IX_cosmetic_audit_prune
ON cosmetic_audit_log ("OccurredAtUtc");
```

#### Partial Indexes (space-efficient)

```sql
-- 9. Active cosmetic items only (most catalog queries filter IsActive)
CREATE INDEX CONCURRENTLY IX_cosmetic_items_active_catalog
ON cosmetic_items ("Category", "Rarity", "SortOrder")
WHERE "IsActive" = true AND "IsHidden" = false;

-- 10. Non-revoked inventory
CREATE INDEX CONCURRENTLY IX_user_inventory_active
ON user_cosmetic_inventory ("UserId", "CosmeticItemId")
WHERE "IsRevoked" = false;
```

---

## PART 7 — WRITE LOAD REDUCTION

### Current Write Hotspots

| Write path | Frequency | Tables modified | Optimization |
|-----------|:---------:|:----------------|:-------------|
| XP award (per answer) | ~50/s | UserProfiles, UserXpEvents, SchoolScoreAggregates | **Buffer + batch** |
| School rank recompute | Per XP event | SchoolScoreAggregates (all rows in period) | **Defer to periodic job** |
| Cosmetic telemetry | Per cosmetic event | cosmetic_telemetry_events | **Buffer + batch insert** |
| Answer recording | Per answer | UserAnswers, UserQuestionStats | **Already optimal (single SaveChanges)** |
| Audit logging | Per admin action | cosmetic_audit_log | **Already low frequency** |

### Strategy 1: Decouple School Aggregation from XP Events

**Current:** Every `AddXpAsync()` → `ApplyXpChangeAsync()` → recomputes school metrics synchronously.

**Proposed:** 
- Remove `ApplyXpChangeAsync()` from the XP hot path entirely
- School aggregation runs **only** in the 10-minute Hangfire job (already exists)
- XP events only update `UserProfiles.DailyXp/WeeklyXp/MonthlyXp/Xp` (already atomic)
- School rankings become eventually consistent (max 10-min stale — already the case for most users)

**Reduction:** Eliminates 8–12 queries per XP event × 50 events/sec = **400–600 Q/s eliminated**.

### Strategy 2: Telemetry Write Buffering

```csharp
// Instead of: await db.CosmeticTelemetryEvents.AddAsync(event); await db.SaveChangesAsync();
// Use:        _telemetryBuffer.Enqueue(event);
// Flush:      Every 5 seconds or when buffer reaches 100 items
//             INSERT INTO cosmetic_telemetry_events VALUES (...), (...), ... (batch)
```

**Reduction:** 50 individual INSERTs → 1 batch INSERT every 5 seconds.

### Strategy 3: Consolidate SaveChangesAsync Calls

AdaptiveLearningService has 5 `SaveChangesAsync()` in one method. Consolidate to 1: 

```csharp
// Stage all changes, then:
await db.SaveChangesAsync(); // Once at the end
```

**Reduction:** 5 round-trips → 1 round-trip per adaptive session answer.

---

## PART 8 — EVENT-DRIVEN AGGREGATION

### Current Architecture

```
Quiz Answer → XP Award → UserProfile.Xp++ (sync)
                        → SchoolAggregation.Recompute (sync, EXPENSIVE)
                        → CosmeticReward.Check (sync, N+1)
```

### Proposed Event-Driven Architecture

```
Quiz Answer → XP Award → UserProfile.Xp++ (sync, fast)
                        → Publish XpAwardedEvent to Channel
                        
XpAwardedEvent → [Background Consumer]
                  ├─ SchoolAggregation (batched, every 10 min — already exists)
                  ├─ CosmeticReward.Check (batched per-user, every 30s)
                  ├─ Telemetry write (buffered batch)
                  └─ LeaderboardSnapshot update (incremental)
```

### Implementation: Use Hangfire + In-Memory Channel

```
1. XP event writes UserProfile atomically (1 query)
2. Publishes to System.Threading.Channels.Channel<XpEvent>
3. Background hosted service drains channel every N seconds
4. Batches events per user, deduplicates, updates projections
```

**No new infrastructure required** — Hangfire already deployed.

### Aggregation Flow

| Event | Source | Aggregate Updated | Frequency |
|-------|--------|:-----------------:|:---------:|
| XpAwarded | Quiz answer, practice | `leaderboard_snapshot.score += delta` | Buffered (5s) |
| XpAwarded | Quiz answer | `SchoolScoreAggregates` | Hangfire (10 min) |
| LevelUp | XP threshold | `user_reward_state.eligible = true` | On-demand |
| StreakUpdated | Daily login | `leaderboard_snapshot.streak` | On-demand |
| AvatarChanged | User action | `user_appearance_projection` | Immediate (user-initiated, low freq) |
| CosmeticGranted | Reward rule | `user_cosmetic_inventory`, `user_reward_state` | Batched (30s) |

---

## PART 9 — DATA PARTITIONING

### Candidates for Partitioning

| Table | Current est. rows | Growth rate | Partition strategy |
|-------|:-----------------:|:----------:|:------------------:|
| **cosmetic_telemetry_events** | 1M+ | ~100k/month | **Range by month** on `OccurredAtUtc` |
| **cosmetic_audit_log** | 100k+ | ~10k/month | **Range by month** on `OccurredAtUtc` |
| **UserAnswers** | 5M+ | ~500k/month | **Range by month** on `AnsweredAt` |
| **UserXpEvents** | 2M+ | ~200k/month | **Range by month** on `AwardedAtUtc` |
| **SchoolRankHistories** | 50k+ | ~5k/month | **Range by month** on `SnapshotTimeUtc` |
| **SyncEventLogs** | 500k+ | ~50k/month | **Range by month** on `ReceivedAtUtc` |

### Implementation: PostgreSQL Declarative Partitioning

```sql
-- Example for cosmetic_telemetry_events
CREATE TABLE cosmetic_telemetry_events_partitioned (
    LIKE cosmetic_telemetry_events INCLUDING ALL
) PARTITION BY RANGE ("OccurredAtUtc");

CREATE TABLE cosmetic_telemetry_events_2026_01 PARTITION OF cosmetic_telemetry_events_partitioned
    FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');
-- Auto-create future partitions via pg_partman or Hangfire job
```

### Retention Policy

| Table | Retain | Archive | Drop |
|-------|:------:|:-------:|:----:|
| cosmetic_telemetry_events | 90 days hot | 1 year cold | After 1 year |
| cosmetic_audit_log | 1 year hot | — | After 2 years |
| UserAnswers | 1 year hot | Archive to S3 | After 2 years |
| SchoolRankHistories | 6 months hot | — | After 1 year |
| SyncEventLogs | 30 days hot | — | After 90 days |

---

## PART 10 — BACKGROUND JOBS

### Current Hangfire Jobs

| Job | Cron | Impact | Issue |
|-----|------|--------|-------|
| `school-leaderboard-refresh` | `*/10 * * * *` | 16–20 queries per run | ✅ Reasonable |
| `school-leaderboard-weekly-snapshot` | `0 * * * *` | 2–3 queries | ✅ Fine |
| `school-leaderboard-monthly-snapshot` | `15 */6 * * *` | 2–3 queries | ✅ Fine |
| `practice-daily-aggregation` | `0 2 * * *` | **3N jobs for N active users** | 🔴 PROBLEM |

### Proposed Job Changes

#### 1. Daily Aggregation — Batch Processing Instead of Per-User Jobs

**Current:** Enqueues 3 background jobs per active user (weakness, path, recommendations).  
At 10k active users → **30,000 Hangfire jobs** queued at 2 AM.

**Proposed:**
```
DailyAggregationJob:
  1. Load active user IDs in batch (1 query)
  2. Process in chunks of 100 users
  3. Each chunk: batch-load data, compute, batch-save
  4. Total: ~100 batch operations instead of 30,000 individual Hangfire jobs
```

#### 2. Add New Background Jobs

| Job | Cron | Purpose |
|-----|------|---------|
| `leaderboard-snapshot-refresh` | `*/5 * * * *` | Rebuild `leaderboard_snapshot` projection for all scopes |
| `reward-state-refresh` | `*/1 * * * *` | Process buffered reward checks |
| `telemetry-flush` | `*/10 * * * * *` (10s) | Flush telemetry write buffer |
| `partition-maintenance` | `0 0 1 * *` | Create next month's partitions, drop expired |
| `stale-data-cleanup` | `0 3 * * *` | Prune old sync logs, expired sessions |

---

## PART 11 — EF CORE OPTIMIZATION

### Current State

| Technique | Usage | Status |
|-----------|:-----:|:------:|
| `AsNoTracking()` | ✅ Widely used | Good |
| `AsSplitQuery()` | ✅ Used for Include chains | Good |
| DTO projections (`.Select()`) | ✅ Most endpoints | Good |
| Compiled queries | ❌ Not used anywhere | **Opportunity** |
| Lazy loading | ❌ Not enabled | Good (avoided) |
| `FindAsync` for PK lookup | ✅ Used appropriately | Good |
| `AnyAsync` vs `CountAsync` for existence | Mixed | Needs cleanup |

### Recommended EF Core Optimizations

#### 1. Compiled Queries for Hot Paths

```csharp
// LeaderboardService — most-called query
private static readonly Func<ApiDbContext, bool, int, IAsyncEnumerable<UserProfile>> 
    _getLeaderboardPage = EF.CompileAsyncQuery(
        (ApiDbContext db, bool optIn, int limit) =>
            db.UserProfiles.AsNoTracking()
                .Where(u => u.LeaderboardOptIn == optIn)
                .OrderByDescending(u => u.Xp)
                .Take(limit));

// SchoolAggregation — called 4× per refresh
private static readonly Func<ApiDbContext, string, DateTime, IAsyncEnumerable<SchoolScoreAggregate>> 
    _getAggregatesByPeriod = EF.CompileAsyncQuery(
        (ApiDbContext db, string period, DateTime periodStart) =>
            db.SchoolScoreAggregates.AsNoTracking()
                .Where(x => x.Period == period && x.PeriodStartUtc == periodStart));
```

**Expected impact:** 10–30% latency reduction for compiled query paths (eliminates expression tree compilation overhead per call).

#### 2. Replace `CountAsync` with `AnyAsync` Where Appropriate

Several existence checks use `CountAsync(predicate) > 0` instead of `AnyAsync(predicate)`:
```csharp
// Bad:  if (await db.UserAnswers.CountAsync(x => ...) > 0)
// Good: if (await db.UserAnswers.AnyAsync(x => ...))
```
`AnyAsync` short-circuits at the first match. In PostgreSQL this translates to `SELECT EXISTS(...)` vs `SELECT COUNT(*)`.

#### 3. Batch `SaveChangesAsync` Calls

In `AdaptiveLearningService`, consolidate 5 `SaveChangesAsync()` calls into 1 at the method end. EF Core batches all tracked changes into a single SQL transaction.

#### 4. Use `ExecuteUpdateAsync` for Projection Updates

Instead of load-modify-save patterns for projections:
```csharp
// Instead of:
var profile = await db.UserProfiles.FindAsync(userId);
profile.DailyXp += delta;
await db.SaveChangesAsync();

// Use (EF Core 7+):
await db.UserProfiles
    .Where(p => p.UserId == userId)
    .ExecuteUpdateAsync(s => s.SetProperty(p => p.DailyXp, p => p.DailyXp + delta));
```
Eliminates the SELECT before UPDATE. Single round-trip.

---

## PART 12 — NETWORK REDUCTION

### Current Round-Trip Patterns

| Endpoint | DB Round-Trips | Proposed |
|----------|:--------------:|:--------:|
| `POST /api/quiz/answer` | 18–31 | **3–5** |
| `GET /api/leaderboard` | 7–12 | **1–2** |
| `GET /api/leaderboard/global` | 4 (full scans) | **1** |
| `POST /api/quiz/offline-submit` (10) | 15–18 | **4–5** |
| Cosmetic catalog + inventory | 3–5 | **0–1** (cached) |

### Strategy 1: Aggregate API Responses

Create composite endpoints that return everything a mobile client needs in one call:

```
GET /api/me/dashboard
Returns: {
  profile: { ... },
  dailyProgress: { ... },
  leaderboardPosition: { rank, percentile },
  unclaimedRewards: [ ... ],
  avatarConfig: { ... }
}
```

**Eliminates:** 5 separate API calls from Flutter client → 1 call.

### Strategy 2: Batch Query Execution

Use `Task.WhenAll` for independent queries within a single endpoint:

```csharp
var profileTask = db.UserProfiles.FindAsync(userId);
var inventoryTask = db.UserCosmeticInventories.AsNoTracking()
    .Where(x => x.UserId == userId).ToListAsync();
var configTask = db.UserAvatarConfigs.FindAsync(userId);

await Task.WhenAll(profileTask.AsTask(), inventoryTask, configTask.AsTask());
```

**Reduces sequential wait time** (not query count, but total latency).

### Strategy 3: ETag-Based Conditional Responses

For semi-static data (cosmetic catalog, design tokens):
```
GET /api/cosmetics/items
If-None-Match: "abc123"
→ 304 Not Modified (0 bytes, 0 DB queries)
```

Cache the catalog hash in Redis. Return 304 if unchanged.

---

## PART 13 — LOAD TESTING PLAN

### Workload Profiles

#### Profile A: 10,000 Users (Current Target)

| Action | Rate | Duration |
|--------|:----:|:--------:|
| Quiz answers | 50/sec | Sustained |
| Leaderboard views | 20/sec | Sustained |
| Login/auth | 5/sec | Sustained |
| Cosmetic catalog views | 10/sec | Sustained |
| Offline sync batches | 2/sec | Burst |
| School leaderboard | 5/sec | Sustained |

**Expected metrics (BEFORE optimization):**
- DB queries/sec: ~1,500–2,200
- DB CPU: 60–80%
- P95 latency: 200–500ms
- Memory: 2–4 GB (global leaderboard full-table loads)

**Expected metrics (AFTER optimization):**
- DB queries/sec: ~100–200
- DB CPU: 5–15%
- P95 latency: 20–50ms
- Memory: 500 MB–1 GB
- Cache hit rate: 85–95%

#### Profile B: 100,000 Users

| Action | Rate |
|--------|:----:|
| Quiz answers | 500/sec |
| Leaderboard views | 200/sec |
| All other | 10× Profile A |

**BEFORE:** System cannot handle this. Global leaderboard loads 100k rows into memory. School aggregation GROUP BY scans 100k rows per period.

**AFTER (with all optimizations):**
- DB queries/sec: ~500–1,000
- DB CPU: 15–30%
- P95 latency: 30–80ms
- Redis handles leaderboard reads (sorted sets)
- Background jobs handle aggregation
- Partitioned tables handle historical data

### Key Metrics to Monitor

| Metric | Tool | Alert Threshold |
|--------|------|:---------------:|
| Query latency P95 | PostgreSQL `pg_stat_statements` | > 100ms |
| Active connections | `pg_stat_activity` | > 50 |
| Cache hit rate | Redis INFO + IMemoryCache counters | < 80% |
| Hangfire queue depth | Hangfire dashboard | > 1,000 |
| Table bloat | `pg_stat_user_tables` dead tuples | > 10% |
| Index usage | `pg_stat_user_indexes` | idx_scan = 0 |

---

## PART 14 — OUTPUT SUMMARY

### 1. DB Bottleneck List (Priority Order)

| # | Bottleneck | Impact | Fix |
|---|-----------|--------|-----|
| 1 | **School aggregation on every XP event** (8–12 queries) | ~600 Q/s at 10k users | Decouple to background job |
| 2 | **Cosmetic reward N+1** (3–10 queries per answer) | ~500 Q/s | Batch-load items + claims |
| 3 | **ComputeRank full-table COUNT** (2× per leaderboard view) | ~40 Q/s but O(n) each | Leaderboard projection table |
| 4 | **Global leaderboard full-table load** (3 tables, all rows) | Fatal at scale | Rewrite with indexed LIMIT |
| 5 | **Offline submit N+1 idempotency** (1 query per answer) | ~20 Q/s | Batch existence check |
| 6 | **Daily aggregation job explosion** (3N Hangfire jobs) | 30k jobs at 10k users | Batch processing |
| 7 | **AdaptiveLearning 5× SaveChanges** | 5× transaction overhead | Consolidate to 1 |
| 8 | **Unbounded telemetry/audit growth** | Table bloat over time | Partition + retention |

### 2. Caching Architecture

```
┌───────────────────────────────────────────────────────┐
│                   FLUTTER CLIENT                       │
│           (ETag caching, local SQLite)                 │
└──────────────────────┬────────────────────────────────┘
                       │
┌──────────────────────▼────────────────────────────────┐
│              ASP.NET Core API                          │
│                                                        │
│  L0: HttpContext.Items                                 │
│      UserProfile, UserSettings per-request             │
│                                                        │
│  L1: IMemoryCache (SizeLimit=1000)                    │
│      Cosmetic catalog (5min), Reward rules (5min),     │
│      Question sets (10min), Appearances (30s)          │
│                                                        │
│  L2: Redis                                             │
│      Leaderboard sorted sets (real-time),              │
│      School rankings (2min), Catalog hash (5min),      │
│      Leaderboard pages (30s)                           │
└──────────────────────┬────────────────────────────────┘
                       │
┌──────────────────────▼────────────────────────────────┐
│              PostgreSQL                                 │
│                                                        │
│  Projections: leaderboard_snapshot, user_quiz_summary, │
│               user_reward_state                        │
│  Partitioned: telemetry, audit, answers, xp_events     │
│  Indexes: covering indexes for leaderboard queries     │
└───────────────────────────────────────────────────────┘
```

### 3. Projection Tables

| Table | Source | Update Trigger | Used By |
|-------|--------|:-------------:|---------|
| `leaderboard_snapshot` | UserProfiles | Hangfire (5 min) | All leaderboard endpoints |
| `user_quiz_summary` | UserAnswers + UserQuestionStats | Per-answer increment | Global leaderboard |
| `user_reward_state` | CosmeticRewardRules + Claims | Hangfire (1 min) | Reward processing |
| `user_appearance_projection` | UserAvatarConfigs + CosmeticItems | On avatar change | Leaderboard appearance |
| `SchoolScoreAggregates` | UserProfiles | Hangfire (10 min) | School leaderboard |

### 4. Index Plan

| Priority | Index | Table | Columns | Type |
|:--------:|-------|-------|---------|:----:|
| P0 | Covering leaderboard | UserProfiles | (LeaderboardOptIn, Xp DESC) INCLUDE (DisplayName, Level, Streak) | Covering + Partial |
| P0 | School XP aggregation | UserProfiles | (SchoolId, DailyXp, WeeklyXp, MonthlyXp) WHERE SchoolId IS NOT NULL | Partial |
| P1 | Answer idempotency | UserAnswers | (UserId, QuestionId, AnsweredAt) | Composite |
| P1 | XP deduplication | UserXpEvents | (UserId, SourceType, SourceId) WHERE SourceId IS NOT NULL | Filtered |
| P2 | Weekly answer agg | UserAnswers | (UserId, IsCorrect, AnsweredAt) WHERE IsCorrect | Partial |
| P2 | Active catalog | cosmetic_items | (Category, Rarity, SortOrder) WHERE IsActive AND NOT IsHidden | Partial |
| P2 | Telemetry pruning | cosmetic_telemetry_events | (OccurredAtUtc) | Simple |

### 5. Partition Strategy

| Table | Partition Key | Interval | Retention |
|-------|:------------:|:--------:|:---------:|
| cosmetic_telemetry_events | OccurredAtUtc | Monthly | 90 days hot, 1 year archive |
| cosmetic_audit_log | OccurredAtUtc | Monthly | 1 year |
| UserAnswers | AnsweredAt | Monthly | 1 year hot |
| UserXpEvents | AwardedAtUtc | Monthly | 1 year |
| SyncEventLogs | ReceivedAtUtc | Monthly | 30 days |

### 6. EF Core Query Improvements

| Optimization | Where | Impact |
|-------------|-------|--------|
| `EF.CompileAsyncQuery` for leaderboard/ranking | LeaderboardService | 10–30% latency reduction |
| `ExecuteUpdateAsync` for XP increments | XpTrackingService | 1 round-trip instead of 2 |
| Batch `SaveChangesAsync` (5→1) | AdaptiveLearningService | 80% fewer transactions |
| `AnyAsync` replacing `CountAsync > 0` | Various | Short-circuit optimization |
| Batch N+1 elimination | CosmeticRewards, OfflineSubmit | 3–10× fewer queries |

### 7. Estimated Load Reduction

| Optimization | Current Q/s (10k users) | After Q/s | Reduction Factor |
|-------------|:----------------------:|:---------:|:----------------:|
| Decouple school agg from XP path | 600 | 0.03 (Hangfire only) | **20,000×** |
| Cache cosmetic catalog + rules | 250 | 5 | **50×** |
| Batch cosmetic reward N+1 | 500 | 50 | **10×** |
| Cache leaderboard pages | 240 | 24 | **10×** |
| Rewrite global leaderboard | 20 (O(n)) | 1 (O(limit)) | **20×** (per query cost) |
| Batch offline idempotency | 20 | 2 | **10×** |
| Request-scoped profile cache | 200 | 100 | **2×** |
| Compiled queries | (latency) | (latency) | 10–30% faster |
| **Telemetry write buffering** | 50 writes/s | 0.2 writes/s | **250×** |
| **TOTAL** | **~1,900 Q/s** | **~180 Q/s** | **~10.5×** |

With all optimizations applied:
- **Conservative estimate: 10× reduction** (caching + decoupling school agg + N+1 fixes)
- **Full implementation: 15–20× reduction** (add projections + partitioning + compiled queries)
- **At 100k users: 20–30× reduction** (caching scales logarithmically, projections eliminate O(n) scans)

### Implementation Priority

| Phase | Items | Effort | Impact |
|:-----:|-------|:------:|:------:|
| **1** | Decouple school agg from XP path, batch cosmetic N+1, rewrite global leaderboard | 2–3 days | **8× reduction** |
| **2** | Add L1/L2 caching (catalog, leaderboard pages, reward rules), compiled queries | 3–5 days | **12× reduction** |
| **3** | Leaderboard projection table, user_quiz_summary, request-scoped cache | 3–5 days | **15× reduction** |
| **4** | Table partitioning, telemetry buffering, daily job batching, retention policies | 2–3 days | **18× reduction** |
| **5** | Covering indexes, ETag support, aggregate API endpoints | 2–3 days | **20× reduction** |

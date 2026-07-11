# Backend API and Database Residual Audit — Pass 2 — 2026-07-11

> Static connector-based audit only. This pass continues after `BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11.md`. It records code-level risks and routes them into implementation prompts. It does **not** prove production exploitation and it does **not** claim runtime fixes have landed.

Repo: `ivanjovicic/MathLearning`  
Reviewed head: `6cdff4c7fbeb595ed29fc11b4641d7b9fe488100`  
Scope: remaining mobile-facing backend API, economy/cosmetics, leaderboard, identity/profile/avatar, EF/PostgreSQL transaction behavior  
Excluded: `src/MathLearning.Admin/**`, Admin UI, content-authoring UI and already-owned queue work  
Detailed queue: `docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md`

## Method and duplicate-avoidance

This pass inspected runtime code that was not deeply covered by the first API/DB residual audit. Findings were compared against the central queue, current test/performance queues and the new failing-test/migration queue.

The following existing owners were not duplicated:

- `BACKEND-MIGRATION-001` — historical cosmetics FK-name drift for clean/upgraded PostgreSQL;
- `BACKEND-TEST-012` / `BACKEND-API-DB-007` — refresh-token schema and at-rest lifecycle;
- `BACKEND-TEST-013` — generic missing-operation-identity policy;
- `BACKEND-TEST-014` — existing economy/cosmetics state-machine and canonical payload tests;
- `BACKEND-TEST-022` — durable analytics ingest;
- `BACKEND-TEST-023` — outbox multi-instance correctness;
- `BACKEND-TEST-032/033` — PostgreSQL provider lane and cancellation/rollback matrix;
- `BACKEND-TEST-034` / `BE-PERF-008` — general legacy-route parity/deprecation;
- `BE-PERF-013` / `BACKEND-API-DB-008` — general pure-read cleanup and user query discipline.

New prompts own concrete uncovered trust boundaries or explicitly extend an existing canonical owner.

## Executive priority

| Priority | Finding | Primary risk | Prompt |
|---|---|---|---|
| P0 | Authenticated clients can claim arbitrary active cosmetic items and grant arbitrary non-Daily-Run fragment copies | direct inventory/reward forgery | `BACKEND-API-DB-009` |
| P0 | Active legacy coin/hint/power-up endpoints bypass canonical economy settlement | unlimited coin minting, negative-amount exploit, free paid hints and duplicate spending | `BACKEND-API-DB-010` |
| P0 | Student leaderboard parses Identity string user IDs as integers | normal GUID users fail ranking/cursor paths; endpoint instability | `BACKEND-API-DB-011` |
| P1 | Redis leaderboard and DB fallback implement different scope/pagination semantics | friends may receive global rows, school/faculty keys are wrong, cursor loops/repeats | `BACKEND-API-DB-012` |
| P1 | Anonymous legacy `/auth/register` creates Identity users and refresh tokens without a `UserProfile` | incomplete/orphan accounts and contract divergence | `BACKEND-API-DB-013` |
| P1 | Legacy photo-avatar API uses integer route IDs for string Identity users and local ephemeral storage | route is unusable for normal users; broken public URLs, data loss/orphans across deploys/replicas | `BACKEND-API-DB-014` |
| P0/P1 | Economy/cosmetics pending idempotency rows are committed before domain transactions and have no lease/recovery contract | permanent `transaction_in_progress` after crash/cancellation; blocked legitimate retries | `BACKEND-API-DB-015` |

---

## Finding 1 — Cosmetics mutation endpoints trust client-declared entitlement

### Code evidence

`POST /api/cosmetics/items/{itemKey}/claim`:

- accepts source/sourceType/sourceEvent/metadata from the client;
- checks only whether the item exists and is active;
- inserts ownership when the user does not already own it;
- does not prove a server-settled reward, purchase, milestone, season tier or other entitlement.

`POST /api/cosmetics/fragments/grant` has a protected Daily Run branch, but every non-Daily-Run source accepts a client-provided recognized fragment name and any positive `Copies` value. The client also controls the normalized source and source event.

The legacy cosmetics shop purchase path additionally checks price and retirement, but does not visibly reject inactive, hidden or not-yet-released items and does not use the canonical economy idempotency contract.

### Risk

An authenticated modified client can grant itself full cosmetic ownership or enough fragments to unlock an item. Idempotency prevents duplicate execution of the same keys; it does not establish entitlement. Client-controlled source metadata can make forged grants look like legitimate reward sources.

### Required action

Make all grant/claim/purchase operations consume server-authoritative entitlement records. Public mobile requests may reference a settled entitlement ID but must never specify the reward amount/item/source as authority. Remove or strictly gate generic grant surfaces that are intended only for trusted internal jobs.

---

## Finding 2 — Legacy economy, hint and power-up routes remain authoritative

### Code evidence

`POST /api/coins/earn` accepts an authenticated user's arbitrary `amount` and directly increments `Coins` and `TotalCoinsEarned`.

`POST /api/coins/spend` also trusts `amount`. A negative amount passes the current insufficient-balance check and then executes `Coins -= amount`, which increases the balance while reducing `TotalCoinsSpent`.

`POST /api/powerups/streak-freeze/buy` mutates coins/freezes without stable operation identity or a concurrency-safe canonical settlement.

The `/api/hints/questions/*` formula/clue/solution routes are GET endpoints that deduct coins and insert `UserHint`. Concurrent first reads can race. Separate legacy `/api/questions/{id}/hint/*` routes return formula/clue/elimination results without coin settlement, allowing paid content to be obtained through a compatibility alias.

### Risk

These routes bypass `/api/economy/*`, allowing self-minting, negative-amount exploitation, free paid hints, duplicate charging and divergent balances/history.

### Required action

Disable authoritative mutation in legacy routes. Route supported clients through one canonical economy settlement service with server-owned prices, positive bounds, stable operation identity, exact replay and PostgreSQL concurrency proof. GET endpoints must be read-only.

---

## Finding 3 — Student leaderboard assumes numeric user IDs

### Code evidence

Identity and `UserProfile.UserId` are strings. Current account creation uses ASP.NET Identity string IDs, normally GUID-shaped values.

`StudentLeaderboardService` uses `int.Parse` for:

- cursor filtering;
- first-row rank calculation;
- next-cursor creation;
- current-user rank calculation.

`LeaderboardRankingUtils.ComputeRankAsync` also applies `int.Parse(u.UserId)` in the rank tie-break predicate. `LeaderboardCursor` stores the tiebreak ID as `int`.

### Risk

Normal string/GUID users can receive `FormatException`, query-translation errors or broken cursors on `/api/leaderboard/student`. The issue affects first-page rank computation as well as later pages.

### Required action

Use the canonical string user ID as the deterministic tiebreaker and cursor component. Version the cursor contract so old integer cursors fail safely or remain deliberately compatible. Prove SQL translation and stable keyset pagination on PostgreSQL.

---

## Finding 4 — Redis leaderboard semantics diverge from DB fallback

### Code evidence

The Redis implementation's DTO wrapper forwards only `Scope`, `Period` and `Limit` to a simpler overload. It ignores:

- cursor;
- authenticated user context;
- school/faculty IDs;
- friend IDs.

The friends scope therefore reads a Redis sorted set directly instead of restricting results to the user's friend graph. The generic endpoint returns `NextCursor = cursor`, so it echoes the incoming cursor rather than producing a next-page token. The DB fallback has its own scope filtering and no cursor implementation in the same interface path.

### Risk

Behavior changes depending on whether Redis is available. Friends/school/faculty responses can be logically wrong, retries can repeat the same page, and failover can change ranking membership and rank values.

### Required action

Define one canonical normalized request and one cursor/rank contract shared by Redis and DB implementations. Redis keys must encode the actual scope identity or use an explicit candidate-filter strategy. Add parity tests that run the same fixtures through both implementations.

---

## Finding 5 — Legacy registration creates incomplete accounts

### Code evidence

The `/auth` route group is anonymous. Besides the canonical `/auth/mobile/register`, it also exposes `/auth/register`, despite the comment describing it as Admin/existing behavior.

The legacy registration route:

- creates an `IdentityUser`;
- creates access and refresh tokens;
- does not create a `UserProfile`;
- does not use the mobile registration transaction/compensation path;
- does not apply the same response/profile contract.

### Risk

Anonymous callers can create valid Identity/token records whose profile-dependent API calls then fail. Duplicate registration implementations drift in validation, transactionality and cleanup behavior.

### Required action

Choose one public registration owner. Remove/deprecate the incomplete route or protect a true operator-only route with the exact admin policy and a service that atomically creates all required account state. Existing incomplete users need an explicit repair/reconciliation plan.

---

## Finding 6 — Legacy photo-avatar contract is incompatible with Identity and deployment storage

### Code evidence

The photo-avatar upload/read routes use `/{id:int}` and compare `id.ToString()` with the authenticated string user ID. Normal Identity GUID users can never satisfy this check.

The saved URL contains the string user ID, while the mapped read route expects an integer ID. The read route is owner-authorized even though public profile DTOs expose `AvatarUrl`.

Files are written under the API container's local `uploads/avatars` directory before the profile DB update. Local container storage is not a durable shared object store and no visible compensation removes an orphan if the DB update fails. Multi-replica requests can reach a replica that does not have the file.

### Risk

The endpoint is unusable for normal users, advertised URLs may not resolve, and uploaded files can disappear across deploys or be orphaned/inconsistent with DB state.

### Required action

Decide whether photo avatars remain supported alongside cosmetics avatars. If retained, use string/self routes, a durable storage abstraction, public or signed read semantics, atomic/compensated metadata updates and old-object cleanup. If obsolete, remove the route and stale `AvatarUrl` behavior through a mobile-compatible deprecation.

---

## Finding 7 — Pending idempotency claims can become permanent tombstones

### Code evidence

Both `EconomyTransactionService.BeginOrGetExistingAsync` and `CosmeticsIdempotencyService.BeginOrGetExistingAsync` insert a `Pending` row and immediately call `SaveChangesAsync`.

Endpoint code invokes these begin methods before opening the database transaction that contains the profile/inventory/reward mutation and ledger completion.

Existing pending rows are returned as `transaction_in_progress`. No owner token, lease expiry, next-attempt time or safe stale-pending takeover was found in the reviewed services.

### Failure scenario

The process can stop, the request can be cancelled, or an unexpected exception can occur after the pending row commits but before the domain transaction starts/completes. Every legitimate retry with the same keys then sees a permanent pending record and cannot replay or resume the operation.

### Required action

Define a durable processing-ownership contract. Either keep the claim and authoritative mutation in one safely retriable database transaction, or add an explicit lease/attempt owner with bounded stale recovery. Completion and domain writes must be atomic; takeover must never allow two owners to settle the same operation.

---

## Recommended execution order

1. `BACKEND-API-DB-009` — stop arbitrary cosmetic grants.
2. `BACKEND-API-DB-010` — close coin/hint/power-up compatibility bypasses.
3. `BACKEND-API-DB-011` — restore leaderboard behavior for string Identity IDs.
4. `BACKEND-API-DB-015` — make economy/cosmetics retries recoverable and atomic.
5. `BACKEND-API-DB-012` — align Redis and DB leaderboard semantics/pagination.
6. `BACKEND-API-DB-013` — unify registration ownership and repair incomplete accounts.
7. `BACKEND-API-DB-014` — retire or repair the legacy photo-avatar contract.
8. Continue existing `BACKEND-MIGRATION-001`, `BACKEND-TEST-032/033/034` and first-pass API/DB prompts according to the central queue.

## Validation status

- Static GitHub connector review completed against the recorded head.
- The latest backend test repair commit reports 996/996 tests green, but those tests do not prove the new findings are fixed.
- No runtime code, schema or mobile code was changed by this audit.
- No new `dotnet`, PostgreSQL concurrency, query-plan, Redis integration or object-storage validation was executed.
- Every implementation prompt requires executable evidence before a finding may be marked fixed or validated.

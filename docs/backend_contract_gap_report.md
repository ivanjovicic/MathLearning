# Backend contract gap report

Last updated: 2026-06-25  
Mobile contract source: `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_api_contract.md`

## Summary

| Endpoint | Operation type | Status | Evidence |
|---|---|---|---|
| `POST /api/quiz/answer` | `quiz_answer` | **Implemented / tested** | `IdempotencyLedger` + `QuizAnswerIdempotencyTests.cs` + `MobileMutationContractIntegrationTests.cs` |
| `POST /api/quiz/srs/update` | `srs_update` | **Implemented / tested** | `SrsUpdateIdempotencyTests.cs` + `MobileMutationContractIntegrationTests.cs` |
| `POST /api/daily-run/chest/claim` | `daily_run_chest_claim` | **Verified (domain-table Policy B)** | `DailyRunChestClaimIdempotencyTests.cs` + endpoint integration tests + `MobileMutationContractIntegrationTests.cs` |
| Economy mutations | various | **Verified** | `EconomyOperationIdIdempotencyTests.cs` + `MobileEconomyContractIntegrationTests.cs` + `MobileMutationContractIntegrationTests.cs` |
| Cosmetics mutations | various | **Verified** | `CosmeticsMutationResponseTests.cs` + cosmetics contract tests + `MobileApiRouteContractTests.cs` |

## `POST /api/daily-run/chest/claim` — Policy B (domain-table idempotency)

**Decision (P0-BE-03):** keep `daily_run_chest_claims` as authority instead of shared `idempotency_ledger`.

- Unique indexes: `(userId, transactionId)` and `(userId, day)`; per-user/day lock for concurrency.
- `transactionId` is the mobile settlement root; client `idempotencyKey` is accepted but not used for dedupe.
- Existing `transactionId` → `200` + `alreadyClaimed: true` (replays stored claim even if request `date` differs).
- Existing `day` with new `transactionId` → `200` + `alreadyClaimed: true`; no second award.
- Same `transactionId` + different `date` → replay original claim, not `409 idempotency_conflict` (intentional).

Tests: `DailyRunChestClaimIdempotencyTests.cs`, `DailyRunChestClaimEndpointTests.cs`, `DailyRunEndpointsIntegrationTests.cs`
Contract coverage: `MobileMutationContractIntegrationTests.cs`, `MobileApiRouteContractTests.cs`

Helper: `DailyRunChestClaimIdempotency.cs`

## Economy mutations — `economy_transactions` ledger

- `EconomyEndpointHelpers.TryBeginAsync` passes `operationId` (falls back to `transactionId` or `idempotencyKey`).
- Scope: `userId + transactionType + operationId/idempotencyKey` via `EconomyTransactionService`.
- Duplicate same payload: replays stored result with `alreadyProcessed` / `alreadyClaimed`.
- Same key, different payload: `409` + `errorCode: idempotency_conflict`.
- Covered paths: coins spend, hints use, rewards claim, streak-freeze purchase, season claims, admin grant.

Tests: `EconomyOperationIdIdempotencyTests.cs`, `EconomySettlementEndpointsIntegrationTests.cs`, `MobileEconomyContractIntegrationTests.cs`
Contract coverage: `MobileMutationContractIntegrationTests.cs`, `MobileApiRouteContractTests.cs`

## Cosmetics mutations — `cosmetics_idempotency_ledger`

- Item claim and fragment grant persist domain state with `SaveChanges` before building mutation `inventory` / `fragmentProgress` snapshots.
- `CosmeticItemClaimRequest` binds `sourceType` and `sourceEvent`; canonical hash includes `source`, `sourceEvent`, and `metadata`.
- Same keys with different `sourceEvent` (even without metadata change) → `409` + `idempotency_conflict`.
- Fragment grant hash also includes `sourceEvent` when provided.

Tests: `CosmeticsMutationResponseTests.cs`, `MobileCosmeticsApiIntegrationTests.cs`, `MobileCosmeticsContractIntegrationTests.cs`
Contract coverage: `MobileApiRouteContractTests.cs`

## `POST /api/quiz/answer` — implemented

- Shared table: `idempotency_ledger` scoped by `user_id + operation_type + operation_id/idempotency_key`.
- Payload hash fields: `quizId`, `questionId`, `answer`, `timeSpentSeconds`.
- Duplicate same payload: replays stored `200` body with `alreadyProcessed: true`.
- Same keys, different payload: `409` + `errorCode: idempotency_conflict`.
- Failed domain mutation inside serializable transaction rolls back pending ledger row (no `completed` row).
- Requests without `operationId`/`idempotencyKey` keep legacy behavior (no ledger).

Tests: `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs`
Contract coverage: `tests/MathLearning.Tests/Contracts/MobileMutationContractIntegrationTests.cs`, `tests/MathLearning.Tests/Endpoints/MobileApiRouteContractTests.cs`

Migration: `20260625080422_AddIdempotencyLedger`

## Shared idempotency infrastructure direction (U1)

### Recommendation

Keep split ledgers/domain authorities for now, but extract shared helper logic for canonical payload hashing,
duplicate/conflict mapping, and replay-state modeling.

Why this is the lowest-risk direction:

- `IdempotencyLedgerService` and `CosmeticsIdempotencyService` already share nearly identical
  begin/replay/conflict/write logic, but they differ in schema shape and stored result semantics.
- `EconomyTransactionService` follows the same flow, but keeps enum status, nullable `OperationId`, and
  economy-specific naming (`TransactionType`, `TransactionId`) that many settled endpoints already rely on.
- `DailyRunChestClaimIdempotency` is intentionally not a generic ledger. Its authority is the domain claim row plus
  `(userId, day)` uniqueness, and its replay semantics are "replay by business identity" rather than "same key or
  conflict".
- Quiz and SRS are the only current consumers that cleanly fit the new shared `idempotency_ledger` contract without
  domain-specific exceptions.

Short version: share the algorithm, not the storage table.

### Pattern comparison

| Area | User scope | Identity scope | Conflict check | Stored result | Failure/rollback model | Should stay domain-specific? |
|---|---|---|---|---|---|---|
| Economy | `userId + transactionType + operationId/idempotencyKey` | dual lookup, unique indexes by type | request hash mismatch -> `409` | JSON result on ledger row | pending/completed/failed row kept in `economy_transactions` | Partial |
| Cosmetics | `userId + operationId/idempotencyKey` | dual lookup, unique indexes without type in keys | payload hash mismatch -> `409` | JSON result on ledger row | pending/completed/failed row kept in `cosmetics_idempotency_ledger` | Partial |
| Quiz/SRS shared ledger | `userId + operationType + operationId/idempotencyKey` | dual lookup, explicit endpoint + http status | payload hash mismatch -> `409` | JSON result + HTTP status | pending/completed/failed row in `idempotency_ledger`; endpoint may roll back transaction | Good shared fit |
| Daily Run chest | `userId + transactionId` and `userId + day` | claim row is authority | no generic payload-hash conflict path; replay original claim | claim entity itself | DB uniqueness + endpoint transaction + retry fallback query | Yes |

### Shareable concerns without schema migration

These are good shared-helper candidates:

- canonical JSON normalization and SHA-256 hashing
- "find by operation id / find by idempotency key / ensure same row" algorithm
- common conflict exception metadata (`userId`, operation type, operation id, idempotency key)
- replay-state projection (`pending`, `completed`, `failed`, result json, error code, timestamps)
- helper for race recovery after unique-index `DbUpdateException`

These should remain domain-owned:

- table/entity shape and index definitions
- status enum/string choice for existing tables
- endpoint-specific replay payloads and HTTP status policy
- Daily Run replay-by-day semantics
- economy-specific transaction naming and existing migration history
- cosmetics-specific `sourceEvent` and inventory snapshot rules

### Minimal follow-up shape

Safe next step is not a new universal table or broad migration. Safe next step is a small internal helper layer used by
services that already behave alike.

Suggested follow-up files:

- `src/MathLearning.Infrastructure/Services/Idempotency/IdempotencyPayloadCanonicalizer.cs`
- `src/MathLearning.Infrastructure/Services/Idempotency/IdempotencyRaceResolver.cs`
- `src/MathLearning.Infrastructure/Services/Idempotency/IdempotencyReplayState.cs`
- `src/MathLearning.Infrastructure/Services/EconomyTransactionService.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticsIdempotencyService.cs`
- `src/MathLearning.Infrastructure/Services/IdempotencyLedgerService.cs`

Suggested rule for future PRs:

- Use `IdempotencyLedgerService` for new endpoints only when the endpoint semantics are
  "same scoped keys + same payload -> replay stored response; same scoped keys + different payload -> conflict".
- Stay on a domain-owned authority when the endpoint replay semantics are driven by a business identity other than the
  submitted keys, as with Daily Run chest.

### Migration risk assessment

Low risk:

- extract canonical JSON/hash helpers
- extract race/conflict helper methods
- add cross-service tests proving identical conflict/replay behavior

Medium risk:

- normalize `EconomyTransactionService` and `CosmeticsIdempotencyService` onto a shared internal base/helper API while
  preserving current tables

High risk:

- migrate economy or cosmetics rows into `idempotency_ledger`
- force Daily Run chest onto generic ledger semantics
- change unique index shapes or rename columns on already-settled production authorities

### Tests required before any shared refactor

- unit tests covering canonical JSON/hash equality across economy, cosmetics, and shared ledger services
- service-level tests for dual-key collision handling and race recovery in all three services
- regression tests proving existing `409 idempotency_conflict` behavior does not change
- regression tests proving completed-row replay returns the same settled body as before
- explicit test proving Daily Run chest remains outside generic conflict semantics

## Authenticated-user scope audit for mutating endpoints (U2)

### Audit result

Targeted mobile-facing mutating endpoints currently resolve mutation scope from authenticated server user id for
standard user flows. No P0/P1 mobile mutation endpoint in the audited set was found to trust a request-body `userId`
or route `userId` as the primary mutation authority.

Intentional exceptions remain only for admin-targeted routes, where:

- the acting user comes from auth context
- the target user may come from route/body
- authorization policy is admin-only

### Endpoint audit table

| Endpoint/group | User source for mutation scope | Safe/unsafe | Test coverage | Follow-up |
|---|---|---|---|---|
| `POST /api/quiz/answer` | authenticated `userId` from auth claim | Safe | `QuizAnswerIdempotencyTests.cs` | None |
| `POST /api/quiz/srs/update` | authenticated `userId` from auth claim | Safe | `SrsUpdateIdempotencyTests.cs` | None |
| `POST /api/quiz/offline-submit` | authenticated `userId` from auth claim | Safe | quiz/sync tests cover user-bound ingest paths | None |
| `POST /api/quiz/batch-submit` | authenticated `userId` from auth claim | Safe | quiz/sync tests cover user-bound ingest paths | None |
| `POST /api/daily-run/chest/claim` | authenticated `userId` via `EndpointUser.GetUserId(ctx)` | Safe | `DailyRunChestClaimIdempotencyTests.cs`, endpoint integration tests | None |
| `POST /api/economy/coins/spend` | authenticated `userId` via `EndpointUser.GetUserId(ctx)` | Safe | economy integration + idempotency tests | None |
| `POST /api/economy/hints/use` | authenticated `userId` via `EndpointUser.GetUserId(ctx)` | Safe | economy integration + idempotency tests | None |
| `POST /api/economy/rewards/claim` | authenticated `userId` via `EndpointUser.GetUserId(ctx)` | Safe | economy integration + contract tests | None |
| `POST /api/shop/streak-freeze/purchase` | authenticated `userId` via `EndpointUser.GetUserId(ctx)` | Safe | economy integration + idempotency tests | None |
| `POST /api/seasons/daily-run-claim` | authenticated `userId` via `EndpointUser.GetUserId(ctx)` | Safe | economy integration + contract tests | None |
| `POST /api/seasons/milestones/{milestoneId}/claim` | authenticated `userId` via `EndpointUser.GetUserId(ctx)` | Safe | economy integration tests | None |
| `PUT /api/cosmetics/avatar` | authenticated `userId` via `EndpointUser.GetUserId(ctx)` | Safe | cosmetics API/contract tests | None |
| `POST /api/cosmetics/items/{itemKey}/claim` | authenticated `userId` via `EndpointUser.GetUserId(ctx)` | Safe | cosmetics contract + mutation response tests | None |
| `POST /api/cosmetics/fragments/grant` | authenticated `userId` via `EndpointUser.GetUserId(ctx)` | Safe | cosmetics contract + mutation response tests | None |
| `PATCH /api/users/{userId}/settings` | route `userId` must equal authenticated `userId`; writes use authenticated id | Safe | `UserSettingsEndpointsIntegrationTests.cs` | None |
| `POST /api/devices/register` | authenticated `userId` passed to service | Safe | `SyncServiceTests.cs` | None |
| `POST /api/sync` | authenticated `userId`; each operation payload also revalidated against auth user in `ValidateOperationEnvelopeAsync` | Safe | `SyncServiceTests.cs` | Keep rejecting mismatched payload user ids |
| `POST /api/admin/economy/rewards/grant` | actor from auth; target user from request body | Safe with documented admin exception | `EconomySettlementEndpointsIntegrationTests.cs` | Keep admin-only |
| `POST /api/leaderboard/admin/add-xp/{userId}` | target user from route; admin-only auth policy | Safe with documented admin exception | admin endpoint coverage is limited | Add explicit admin audit tests if this route becomes release-critical |
| `POST /api/leaderboard/admin/reset-xp/{userId}` | target user from route; admin-only auth policy | Safe with documented admin exception | admin endpoint coverage is limited | Add explicit admin audit tests if this route becomes release-critical |
| Legacy `/api/coins/earn` and `/api/coins/spend` | authenticated `userId` from auth claim | Safe but legacy | legacy coverage limited | Keep in deprecation plan; do not expand usage |

### Idempotency ledger scope notes

- `IdempotencyLedgerService` scopes lookup by `userId + operationType + operationId/idempotencyKey`.
- `EconomyTransactionService` scopes lookup by `userId + transactionType + operationId/idempotencyKey`.
- `CosmeticsIdempotencyService` scopes lookup by `userId + operationId/idempotencyKey`.
- `DailyRunChestClaimIdempotency` scopes replay by domain authority: `userId + transactionId` and `userId + day`.
- Sync transport additionally rejects payloads whose `operation.UserId` differs from authenticated user id before
  domain processing.

### Findings

- No unsafe cross-user mutation path was found in the audited mobile settlement/profile/sync routes.
- The strongest defense-in-depth path is sync, where payload user id is explicitly compared against authenticated user
  id and rejected on mismatch.
- Admin grant/reset paths intentionally support acting-on-behalf-of behavior and are only acceptable because policy is
  admin-gated and audit data records the actor.

### Evidence added in this pass

- Existing route-scope tests: `UserSettingsEndpointsIntegrationTests.cs`
- Existing admin actor/target tests: `EconomySettlementEndpointsIntegrationTests.cs`
- New payload mismatch regression: `SyncServiceTests.cs` (`user_mismatch` rejected)

## Mobile contract HTTP pack (U3)

Focused HTTP-level contract coverage for mobile-facing settlement routes now lives in:

- `tests/MathLearning.Tests/Endpoints/MobileApiRouteContractTests.cs`
- `tests/MathLearning.Tests/Contracts/MobileMutationContractIntegrationTests.cs`
- `tests/MathLearning.Tests/Contracts/MobileEconomyContractIntegrationTests.cs`
- `tests/MathLearning.Tests/Contracts/MobileCosmeticsContractIntegrationTests.cs`

This pack intentionally checks:

- protected mobile routes reject unauthenticated requests
- canonical mobile request shapes bind and settle successfully
- duplicate idempotent requests replay settled responses
- same keys with different payloads return `409` + `idempotency_conflict` where endpoint semantics use generic key/payload conflict

Daily Run chest remains the deliberate exception to generic conflict semantics:

- same `transactionId` replay returns settled success
- same day/new transaction replays prior claim as `alreadyClaimed`
- no generic `idempotency_conflict` assertion is required for this Policy B flow

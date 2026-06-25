# Backend contract gap report

Last updated: 2026-06-25  
Mobile contract source: `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_api_contract.md`

## Summary

| Endpoint | Operation type | Status | Evidence |
|---|---|---|---|
| `POST /api/quiz/answer` | `quiz_answer` | **Implemented / tested** | `IdempotencyLedger` + `QuizAnswerIdempotencyTests.cs` (5 tests) |
| `POST /api/quiz/srs/update` | `srs_update` | **Implemented / tested** | `SrsUpdateIdempotencyTests.cs` (5 tests) |
| `POST /api/daily-run/chest/claim` | `daily_run_chest_claim` | **Verified (domain-table Policy B)** | `DailyRunChestClaimIdempotencyTests.cs` + endpoint integration tests |
| Economy mutations | various | **Verified** | `EconomyOperationIdIdempotencyTests.cs` + existing settlement/contract tests |
| Cosmetics mutations | various | **Verified** | `CosmeticsMutationResponseTests.cs` + cosmetics contract tests |

## `POST /api/daily-run/chest/claim` — Policy B (domain-table idempotency)

**Decision (P0-BE-03):** keep `daily_run_chest_claims` as authority instead of shared `idempotency_ledger`.

- Unique indexes: `(userId, transactionId)` and `(userId, day)`; per-user/day lock for concurrency.
- `transactionId` is the mobile settlement root; client `idempotencyKey` is accepted but not used for dedupe.
- Existing `transactionId` → `200` + `alreadyClaimed: true` (replays stored claim even if request `date` differs).
- Existing `day` with new `transactionId` → `200` + `alreadyClaimed: true`; no second award.
- Same `transactionId` + different `date` → replay original claim, not `409 idempotency_conflict` (intentional).

Tests: `DailyRunChestClaimIdempotencyTests.cs`, `DailyRunChestClaimEndpointTests.cs`, `DailyRunEndpointsIntegrationTests.cs`

Helper: `DailyRunChestClaimIdempotency.cs`

## Economy mutations — `economy_transactions` ledger

- `EconomyEndpointHelpers.TryBeginAsync` passes `operationId` (falls back to `transactionId` or `idempotencyKey`).
- Scope: `userId + transactionType + operationId/idempotencyKey` via `EconomyTransactionService`.
- Duplicate same payload: replays stored result with `alreadyProcessed` / `alreadyClaimed`.
- Same key, different payload: `409` + `errorCode: idempotency_conflict`.
- Covered paths: coins spend, hints use, rewards claim, streak-freeze purchase, season claims, admin grant.

Tests: `EconomyOperationIdIdempotencyTests.cs`, `EconomySettlementEndpointsIntegrationTests.cs`, `MobileEconomyContractIntegrationTests.cs`

## Cosmetics mutations — `cosmetics_idempotency_ledger`

- Item claim and fragment grant persist domain state with `SaveChanges` before building mutation `inventory` / `fragmentProgress` snapshots.
- `CosmeticItemClaimRequest` binds `sourceType` and `sourceEvent`; canonical hash includes `source`, `sourceEvent`, and `metadata`.
- Same keys with different `sourceEvent` (even without metadata change) → `409` + `idempotency_conflict`.
- Fragment grant hash also includes `sourceEvent` when provided.

Tests: `CosmeticsMutationResponseTests.cs`, `MobileCosmeticsApiIntegrationTests.cs`, `MobileCosmeticsContractIntegrationTests.cs`

## `POST /api/quiz/answer` — implemented

- Shared table: `idempotency_ledger` scoped by `user_id + operation_type + operation_id/idempotency_key`.
- Payload hash fields: `quizId`, `questionId`, `answer`, `timeSpentSeconds`.
- Duplicate same payload: replays stored `200` body with `alreadyProcessed: true`.
- Same keys, different payload: `409` + `errorCode: idempotency_conflict`.
- Failed domain mutation inside serializable transaction rolls back pending ledger row (no `completed` row).
- Requests without `operationId`/`idempotencyKey` keep legacy behavior (no ledger).

Tests: `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs`

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

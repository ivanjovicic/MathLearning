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

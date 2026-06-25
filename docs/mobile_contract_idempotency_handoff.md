# Mobile contract idempotency handoff

Backend verification checklist for mobile idempotent mutations.  
Canonical mobile contract: `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_api_contract.md`

## Scope

```text
userId (bearer token) + operationType + operationId/idempotencyKey
```

## Shared ledger (`idempotency_ledger`)

Reusable service: `IIdempotencyLedgerService` / `IdempotencyLedgerService`.

| Column | Purpose |
|---|---|
| `user_id` | From auth context only |
| `operation_type` | e.g. `quiz_answer`, `srs_update` |
| `operation_id`, `idempotency_key` | Client keys (may be identical) |
| `payload_hash` | SHA-256 of canonical idempotency payload |
| `result_json` | Stored success body |
| `status` | `pending` / `completed` / `failed` |

## Verified endpoints

### `POST /api/quiz/answer` (`quiz_answer`)

- Keys optional for legacy clients; when either key is sent, both resolve (single key duplicated).
- Hash: `quizId`, `questionId`, `answer`, `timeSpentSeconds`.
- Tests: `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs`.

### `POST /api/quiz/srs/update` (`srs_update`)

- Reuses shared `idempotency_ledger` via `IIdempotencyLedgerService`.
- Hash: `questionId`, `isCorrect`, `timeMs`.
- Tests: `tests/MathLearning.Tests/Idempotency/SrsUpdateIdempotencyTests.cs`.

### `POST /api/daily-run/chest/claim` (`daily_run_chest_claim`) — Policy B

Domain-table idempotency (not shared ledger). See `DailyRunChestClaimIdempotency.cs`.

- Authority: `daily_run_chest_claims` unique on `(userId, transactionId)` and `(userId, day)`.
- Duplicate `transactionId` or already-claimed `day` → `200` + `alreadyClaimed: true`.
- Same `transactionId`, different request `date` → replay original claim (transaction anchor), not `409`.
- Concurrent claims: per-user/day lock + DB uniqueness.
- Tests: `DailyRunChestClaimIdempotencyTests.cs`.

### Economy mutations (`economy_transactions`)

- `operationId` wired through `EconomyEndpointHelpers.TryBeginAsync` → `EconomyTransactionService`.
- Fallback: `operationId` → `transactionId` → `idempotencyKey`.
- Tests: `EconomyOperationIdIdempotencyTests.cs`.

### Cosmetics mutations (`cosmetics_idempotency_ledger`)

- Mutation responses load `inventory` after `SaveChanges` so newly granted items appear without a follow-up GET.
- Item claim binds `sourceEvent` for idempotency hash (not metadata-only).
- Tests: `CosmeticsMutationResponseTests.cs`.

## Next P1

- Mobile storage/guest verification (P1-MOB-01).

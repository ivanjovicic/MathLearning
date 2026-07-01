# Quiz answer and offline replay transaction audit

Last aligned: 2026-07-01
Prompt: `BE-PERF-003`
Endpoints: `POST /api/quiz/answer`, `POST /api/quiz/offline-submit`, `POST /api/quiz/batch-submit`

## Summary

All three paths still share `ProcessAnswerAttemptWithinTransactionAsync` for the domain mutation.
Offline paths wrap the batch in one serializable transaction through `ApiDbTransactionHelpers.ExecuteWithSerializableRetryAsync`.
The online idempotent answer path now skips `UserSettings` and question graph reads on replay/conflict and only loads them for fresh processing.

Duplicate offline answers still dedupe by `(userId, questionId, answeredAt)` before mutation.
First-correct XP is still guarded by in-transaction stat checks plus the unique index `UX_UserAnswerAudits_FirstCorrect_PerQuestion` with serializable retry on violation.

## `POST /api/quiz/answer` with `operationId` + `idempotencyKey`

### Timeline

```text
1. BEGIN serializable transaction (max 3 retries)
2.   idempotency_ledger BeginOrGetExisting (quiz_answer)
3.   If replay -> return stored JSON (alreadyProcessed)
4.   If pending/conflict -> short-circuit before question/lang load
5.   Load question graph only for fresh processing
6.   Resolve user language only for fresh processing
7.   EnsureQuizSessionAsync
8.   ProcessAnswerAttemptWithinTransactionAsync
9.   BuildSubmitAnswerResponseAsync
10.  idempotency_ledger CompleteAsync with response body
11.  SaveChanges + COMMIT
12. [OUTSIDE TX] ingestService.IngestAttemptsAsync when a new row was imported
```

Legacy path without idempotency keys keeps the same domain mutation, but it loads the question before the serializable retry block and resolves language only when the response body is needed.

### DB call classification

| Call | In TX | Required | Notes |
|---|---|---|---|
| `idempotency_ledger` begin/complete | Yes | Yes | Must commit or roll back with the mutation |
| Question load for fresh idempotent processing | Yes | Yes | Needed only when the request is not a replay |
| `UserSettings` load | Yes | Yes | Needed only when a fresh response body must be built |
| `QuizSessions` existence check | Yes | Yes | Read-only check; insert if missing |
| `UserAnswers` duplicate check | Yes | Yes | Replay guard on `(userId, questionId, answeredAt)` |
| `UserAnswerAudits` replay lookup | Yes | Yes | Restores settled duplicate response |
| `UserQuestionStats` `FOR UPDATE` | Yes | Yes | Attempt counting and first-correct detection |
| `UserProfiles` load/update | Yes | Yes | XP and activity-day updates |
| `XpTrackingService.AddXpWithinTransactionAsync` | Yes | Yes | First-correct award remains authoritative |
| `UserAnswers` / `UserAnswerAudits` insert | Yes | Yes | Mutation and audit trail |
| Explanation lookup for incorrect answers | Yes | Yes | Still inside the fresh-processing path for response shaping |
| `ingestService.IngestAttemptsAsync` | No | Yes when imported | Post-commit side effect |

### Retry policy

- EF concurrency, PostgreSQL serialization failure, or `UX_UserAnswerAudits_FirstCorrect_PerQuestion` unique violation retry up to 3 times.
- Ledger rollback on any unhandled exception before commit still prevents a completed row from being left behind.

## `POST /api/quiz/offline-submit` and `POST /api/quiz/batch-submit`

Both delegate to `HandleOfflineBatchSubmitAsync` and then `ProcessOfflineBatchAsync`.

### Timeline

```text
1. BEGIN serializable transaction
2.   Resolve session id
3.   QuizSessions read (AsNoTracking) or insert
4.   LoadExistingAnswerKeysAsync to prefetch duplicate keys
5.   Questions dictionary load (AsNoTracking)
6.   For each answer:
       - skip if answerKey already seen in batch or DB prefetch
       - skip unknown questionId
       - ProcessAnswerAttemptWithinTransactionAsync
7.  SaveChanges + COMMIT
8. [OUTSIDE TX] ingestService.IngestAttemptsAsync
9. [OUTSIDE TX] CalculateUserOverview for response
```

### DB call classification

| Call | In TX | Required | Notes |
|---|---|---|---|
| Session read | Yes | Yes | AsNoTracking; insert tracked when missing |
| Existing answer key prefetch | Yes | Yes | Prevents per-row duplicate work |
| Questions batch load | Yes | Yes | AsNoTracking; one graph fetch for the batch |
| Per-answer mutation helper | Yes | Yes | Same authoritative mutation as the online path |
| Overview calculation | No | Yes | Response enrichment only |

### Dedupe semantics

- In-batch: `seenAnswerKeys` is keyed by `questionId + answeredAt`.
- Cross-replay: `LoadExistingAnswerKeysAsync` plus `ProcessAnswerAttemptWithinTransactionAsync` ensures duplicate rows do not import again.
- No ledger: offline/batch still rely on the domain dedupe path, not `idempotency_ledger`.

## Optimization applied in BE-PERF-003

- Replay and conflict handling in `/api/quiz/answer` now exits before `UserSettings` or question graph reads.
- Offline/session duplicate reads already use `AsNoTracking()` where the work is read-only.
- Mutation paths remain tracked where the transaction actually needs to write.

## Test evidence

| Scenario | Test file |
|---|---|
| First success + ledger row | `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs` |
| Duplicate replay + alreadyProcessed | `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs` |
| Payload conflict 409 | `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs` |
| Rollback leaves no completed ledger | `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs` |
| Per-user scope isolation | `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs` |
| Offline replay no double XP | `tests/MathLearning.Tests/Endpoints/OfflineBatchSubmitCompatibilityTests.cs` |
| Batch legacy key replay | `tests/MathLearning.Tests/Endpoints/OfflineBatchSubmitCompatibilityTests.cs` |
| In-batch duplicate collapse | `tests/MathLearning.Tests/Endpoints/OfflineBatchSubmitCompatibilityTests.cs` |
| First-correct concurrency guard | `tests/MathLearning.Tests/Services/XpTrackingConcurrencyIntegrationTests.cs` |
| Mobile contract replay/conflict | `tests/MathLearning.Tests/Contracts/MobileMutationContractIntegrationTests.cs` |

## Residual risks

1. Explanation lookup for incorrect answers still happens inside the fresh-processing path because the response body is stored in the idempotency ledger.
2. Legacy `/api/quiz/answer` without idempotency keys still uses the domain mutation path directly.
3. Offline batch still performs a post-commit overview read to shape the response.

## Next prompt

`BE-PERF-004` - DB-backed leaderboard rank optimization.

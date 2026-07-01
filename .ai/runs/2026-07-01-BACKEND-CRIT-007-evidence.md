# BACKEND-CRIT-007 Evidence

Prompt ID: BACKEND-CRIT-007
Queue: `docs/prompt_queues/backend_critical_risk_prevention.md`
Run mode: implementation/test

## Files changed

- `src/MathLearning.Api/Services/OfflineAnswerTimestampPolicy.cs` (new)
- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- `src/MathLearning.Application/DTOs/Quiz/OfflineBatchSubmitIssue.cs` (new)
- `src/MathLearning.Application/DTOs/Quiz/OfflineBatchSubmitResponse.cs`
- `docs/BACKEND_OFFLINE_TIMESTAMP_POLICY.md` (new)
- `tests/MathLearning.Tests/Services/OfflineAnswerTimestampPolicyTests.cs` (new)
- `tests/MathLearning.Tests/Endpoints/OfflineAnswerTimestampIntegrationTests.cs` (new)

## What was done

- Defined offline replay window: +2 minutes future skew, 90-day max age.
- Normalized offline `answeredAt` to UTC with millisecond precision for duplicate detection.
- Legacy batch-submit now rejects malformed timestamps with `invalid_timestamp` diagnostics instead of silent parse fallback.
- Missing legacy `answeredAt` still imports using server UTC but returns `answered_at_defaulted` issue.
- `OfflineBatchSubmitResponse.issues` reports skipped timestamp rows.
- Anti-cheat window aligned with accepted offline timestamp policy.

## Validation run

```bash
git diff --check
dotnet test --filter "OfflineSubmit|Timestamp|Streak|AntiCheat"
```

**Passed: 25, Failed: 0**

## Test matrix

| Case | Coverage |
|---|---|
| Future timestamp | `OfflineSubmit_FutureTimestamp_IsRejectedWithDiagnostic` |
| Very old timestamp | `OfflineSubmit_VeryOldTimestamp_IsRejectedWithDiagnostic` |
| Malformed timestamp | `BatchSubmit_MalformedTimestamp_ReturnsDiagnosticWithoutImport` |
| Local offset → UTC | `BatchSubmit_LocalOffsetAndUtcEquivalent_AreTreatedAsSameReplay` |
| Precision variants | `OfflineSubmit_PrecisionVariants_CollapseToSingleImport` |
| Policy unit tests | `OfflineAnswerTimestampPolicyTests` |

## Risk prevented

- **offline-time-trust**: bounded, normalized offline timestamps for replay, streak, and anti-cheat.

## Commit SHA

f1188af

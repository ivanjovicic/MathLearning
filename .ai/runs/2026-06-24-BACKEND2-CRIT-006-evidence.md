# BACKEND2-CRIT-006 Evidence

Prompt ID: BACKEND2-CRIT-006
Queue: docs/prompt_queues/backend_second_pass_risk_prevention.md
Agent/tool: Cursor Agent
Model provider: unknown-not-exposed
Run mode: implementation/test + docs/spec
Elapsed time: unknown-not-recorded
Relevant prior mistakes read: BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-EVIDENCE-001
How this run avoids prior mistakes: spec doc + targeted idempotency tests

Commit SHA: 85a87c6
Validation command:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Hangfire|BackgroundJob|SchoolLeaderboard|AntiCheat|Aggregation"
```

Result:

```text
Passed!  - Failed: 0, Passed: 12, Skipped: 0, Total: 12
```

Risk prevented:

- Recurring Hangfire jobs now document explicit business keys, overlap policy, and idempotency behavior.
- Overlapping recurring runs are blocked via `[DisableConcurrentExecution]` on each recurring entry point.
- Anti-cheat ML review claims rows atomically on PostgreSQL (`ExecuteUpdate`) with in-memory fallback for tests.

Deliverables:

- `docs/BACKGROUND_JOB_IDEMPOTENCY_SPEC.md` — job inventory (schedule, business key, overlap, idempotency).
- `PracticeHangfireJobsTests` — daily aggregation re-run enqueues the same per-active-user workload.
- `SchoolLeaderboardSnapshotIdempotencyTests` — duplicate snapshot within 20 minutes does not duplicate history.
- `AnswerPatternAntiCheatServiceTests` — ML review replay does not increment attempts.

Runtime changes:

- `[DisableConcurrentExecution]` on `DailyAggregationJob`, leaderboard refresh/snapshot jobs, and anti-cheat sweep.
- `AnswerPatternAntiCheatService.TryClaimMlReviewAsync` — atomic claim on relational stores.

Follow-up (documented, not migrated):

- Optional unique index on `school_rank_history` snapshot buckets if stricter dedupe is required.

## Mistakes observed

none

## Completion %

95%

## Residual risk

- School leaderboard snapshot dedupe relies on 20-minute window logic; stricter DB unique index deferred.

## Commit SHA

85a87c6
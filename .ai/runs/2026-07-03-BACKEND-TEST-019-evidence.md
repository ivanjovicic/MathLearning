# BACKEND-TEST-019 Evidence

Prompt ID: BACKEND-TEST-019
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: relational service tests
Started from queue status: new P0/P1 coverage gap found by BACKEND-TEST-AUDIT-002

## Goal

Add direct relational coverage for `QuizAttemptIngestService`: empty input, durable attempt writes, topic/subtopic aggregation, accumulation into existing statistics, input normalization, scheduler ordering, cancellation, and rollback after SQL has been issued.

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-IDEM-001

## Guardrails

- Use SQLite relational transactions rather than EF InMemory for rollback proof.
- Assert durable state from a fresh context.
- Inject failure from `SavedChangesAsync` so SQL has already executed inside the transaction.
- Assert the weakness-analysis scheduler is called only after successful commit.
- Do not claim current ingest delivery or duplicate-consumer behavior is idempotent; that remains BACKEND-TEST-022.

## Files inspected

- `src/MathLearning.Api/Services/QuizAttemptIngestService.cs`
- `src/MathLearning.Api/Services/WeaknessAnalysisScheduler.cs`
- `src/MathLearning.Api/Services/WeaknessScoring.cs`
- `src/MathLearning.Application/Services/IQuizAttemptIngestService.cs`
- `src/MathLearning.Application/Helpers/UserIdGuidMapper.cs`
- `src/MathLearning.Domain/Entities/WeaknessAnalysisModels.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- `tests/MathLearning.Tests/Helpers/TestDbContextFactory.cs`

## Files changed

- `tests/MathLearning.Tests/Services/QuizAttemptIngestServiceRelationalTests.cs`
- `docs/BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/prompt_queues/backend_test_followups_2026_07_03.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-019-evidence.md`

## Implemented tests

1. Empty input performs no writes and does not enqueue analysis.
2. New batch persists three attempts, clamps negative time to zero, maps topic/subtopic, computes totals/correct/accuracy/last-attempt, and enqueues exactly one analysis job.
3. Existing topic/subtopic statistics accumulate correctly and an older incoming attempt cannot move `LastAttempt` backwards.
4. Failure from `SavedChangesAsync` after attempt/stat SQL is issued rolls the transaction back and does not enqueue analysis.
5. A pre-cancelled batch persists no attempts/stats and does not enqueue analysis.

## Why this matters

No focused direct tests were found for the service that materializes quiz attempts and weakness aggregates. Without relational rollback and scheduler-order coverage, a failure could leave partial analytics state or enqueue analysis for uncommitted rows.

## Static validation

- SQLite provider is already referenced by the test project.
- Every durable assertion is made through a fresh context.
- Failure interceptor activates only for an added `QuizAttempt`, so setup writes are unaffected.
- Scheduler is a recording fake with no hosted background execution.
- Accuracy assertions match `WeaknessScoring.CalculateAccuracy` rounding.
- No sleeps, external storage, Redis or Hangfire are used.

## Validation not run

No executable .NET repository checkout is available in this connector environment. No passing test or build claim is made.

Required command:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~QuizAttemptIngestServiceRelationalTests"
dotnet build MathLearning.slnx -c Release
```

## Confirmed follow-up problem

The authoritative quiz/offline transaction commits before this service is called. A post-commit ingest failure can therefore leave authoritative answer/XP state without analytics delivery, while replay dedupe may generate no new ingest row. Current `QuizAttempt` rows also use random IDs without a visible natural unique key.

Tracked by:

- `BACKEND-MISTAKE-IDEM-001`;
- `BACKEND-TEST-022` durable/idempotent ingest handoff prompt.

## Residual risk

- Tests are not execution-proven.
- SQLite does not prove PostgreSQL locking/serialization behavior.
- Durable post-commit delivery and duplicate consumer idempotency remain unfixed by this test-only package.

## Completion

88%

## Commit SHAs

- `f3e8dabf32fc448bd175ed61ca9a293d7beca3f7` — start evidence
- `e6eead3051c45d1d103042ecf5c927af17f3b90e` — relational ingest tests
- `d0f6bf3548328f8586b17acb0bb83da50e3483a7` — coverage audit
- `836d9ab016615d8e68aa55787bab2f7c3e5e35fa` — durable ingest follow-up prompt
- `9a9f4b696b479d530ed50e0fb433e03dade6af0d` — mistake ledger
- `c431804ef5bbc2c86d6f8b4da9ec2ab41eec75c7` — central queue reconciliation

## Cross-repo sync

Not applicable for the added tests. BACKEND-TEST-022 must record mobile sync if HTTP success/error semantics change.

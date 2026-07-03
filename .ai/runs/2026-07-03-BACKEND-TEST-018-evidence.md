# BACKEND-TEST-018 Evidence

Prompt ID: BACKEND-TEST-018
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: relational offline-batch atomicity tests
Started from queue status: new P0 gap found during coverage audit

## Goal

Prove `/api/quiz/offline-submit` leaves no partial session, answer, audit, stat, XP, or activity mutation when failure occurs after relational SQL has been issued but before transaction commit.

## Guardrails

- Invoke the real authenticated endpoint.
- Use file-backed SQLite and a failure injected from `SavedChangesAsync`.
- Assert durable state from a fresh scope.
- Assert internal failure details do not leak to the client.
- Retry and exact replay must prove one authoritative mutation.

## Files inspected

- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- `src/MathLearning.Api/Endpoints/ApiDbTransactionHelpers.cs`
- `src/MathLearning.Infrastructure/Services/XpTrackingService.cs`
- `tests/MathLearning.Tests/Endpoints/OfflineBatchSubmitCompatibilityTests.cs`
- `tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs`
- `tests/MathLearning.Tests/MathLearning.Tests.csproj`

## Files changed

- `tests/MathLearning.Tests/Endpoints/OfflineBatchRelationalAtomicityTests.cs`
- `docs/prompt_queues/backend_test_coverage.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-018-evidence.md`

## Implemented tests

1. Failure is injected after the outer answer/audit `SaveChangesAsync` has executed. The transaction must roll back:
   - quiz session;
   - user answer;
   - answer audit;
   - question stat;
   - profile XP and period XP;
   - level and activity-day mutation;
   - any XP event rows.
2. Retry after rollback imports exactly one answer and awards exactly 10 XP.
3. Exact replay imports zero and leaves one session, one answer, one audit, one stat attempt, and unchanged XP.
4. Injected internal error text is asserted absent from the public 500 response.

## Why this is important

`ProcessAnswerAttemptWithinTransactionAsync` calls `AddXpWithinTransactionAsync`, which performs an inner `SaveChangesAsync` before the answer and audit are added. A later failure can therefore only be proven safe by a relational transaction test that verifies earlier profile/stat SQL is also rolled back.

## Static validation

- Test uses the seeded real question graph and correct option identifier.
- Test user and profile are created in the same SQLite database used by the API.
- Interceptor activates only when an added `UserAnswer` is present, so setup and the inner XP save are not interrupted.
- Failure occurs after answer/audit SQL but before transaction commit.
- Verification uses a fresh scope after the failed request.
- Retry/replay assertions match the existing offline response contract (`importedCount`, `newXp`).

## Executable validation not run

No .NET SDK/repository checkout is available in this connector environment. GitHub returned no combined status checks for the test commit.

Required command:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~OfflineBatchRelationalAtomicityTests"
```

Also rerun existing offline suites:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "OfflineSubmit|OfflineBatch|OfflineAnswerTimestamp"
```

## Residual risk

- PostgreSQL serializable isolation and provider-specific retry behavior still need database-validation CI.
- Ingest-service failure occurs after the database transaction commits and remains a separate delivery/reconciliation risk.
- New tests are committed but not executable-proof validated.

## Completion

88%

## Commit SHAs

- `10c82dc7f6322bde9700eedcf08467fea2ddfe2f` — start evidence
- `81d3e7330b057c1f4b592bf25999fd852473299a` — relational offline tests
- `6e717512bbb4992c0a0ce792390c649ff40fc7bf` — queue registration

## Cross-repo sync

Not applicable; existing offline contract is unchanged.

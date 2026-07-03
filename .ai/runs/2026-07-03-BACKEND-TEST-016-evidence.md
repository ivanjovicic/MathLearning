# BACKEND-TEST-016 Evidence

Prompt ID: BACKEND-TEST-016
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: relational transaction-helper tests
Started from queue status: new P0 gap found during coverage audit

## Goal

Protect `ApiDbTransactionHelpers.ExecuteWithSerializableRetryAsync` directly: commit, rollback after SQL was issued, retry after EF concurrency conflict, retry exhaustion, and cancellation.

## Guardrails

- Use SQLite relational provider, not EF InMemory, for transaction proof.
- Assert durable state from a fresh context.
- Avoid sleeps and timing-based races.
- Do not claim PostgreSQL provider equivalence.

## Files inspected

- `src/MathLearning.Api/Endpoints/ApiDbTransactionHelpers.cs`
- `src/MathLearning.Domain/Entities/ApplicationLog.cs`
- `docs/BACKEND_TEST_COVERAGE_STRATEGY.md`
- `tests/MathLearning.Tests/MathLearning.Tests.csproj`

## Files changed

- `tests/MathLearning.Tests/Endpoints/ApiDbTransactionHelpersRelationalTests.cs`
- `docs/prompt_queues/backend_test_coverage.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-016-evidence.md`

## Implemented tests

1. Successful action commits one durable row and returns its result.
2. Action performs an inner `SaveChangesAsync`, then throws; the already-issued SQL is rolled back.
3. First `DbUpdateConcurrencyException` retries with a cleared tracker and persists only attempt two.
4. Three consecutive concurrency failures stop after three attempts and leave no rows.
5. A pre-cancelled token leaves no durable state.

## Why this is important

The helper is shared by quiz answer, SRS, and offline batch mutations. Endpoint-only happy-path tests do not prove its retry count, tracker clearing, rollback-after-SQL, or cancellation behavior.

## Static validation

- Test signatures match the current internal helper API.
- SQLite provider and package already exist in the test project.
- Durable assertions use a fresh `ApiDbContext`.
- The retrying context throws before persistence, while the rollback test explicitly saves inside the transaction before throwing.
- No timing delays or real external services are used.

## Executable validation not run

No .NET SDK/repository checkout is available in this connector environment. GitHub returned no combined status checks for the test commit.

Required command:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~ApiDbTransactionHelpersRelationalTests"
```

Follow with:

```text
dotnet build MathLearning.slnx -c Release
```

## Residual risk

- PostgreSQL serialization-failure and named unique-constraint retry branches still require PostgreSQL-backed CI.
- New tests are committed but not executable-proof validated.

## Completion

88%

## Commit SHAs

- `3e1440b6f37ae70e4e70b34cfbdf861acc7be0de` — start evidence
- `e7db86335249860943ea44309b4585c1a04826cf` — relational helper tests
- `6e717512bbb4992c0a0ce792390c649ff40fc7bf` — queue registration

## Cross-repo sync

Not applicable; backend test-only behavior, no contract change.

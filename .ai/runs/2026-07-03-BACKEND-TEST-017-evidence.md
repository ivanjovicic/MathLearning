# BACKEND-TEST-017 Evidence

Prompt ID: BACKEND-TEST-017
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: relational HTTP registration atomicity tests
Started from queue status: new P0 gap found during coverage audit

## Goal

Prove `/auth/mobile/register` is atomic on a relational provider when failure occurs after Identity/Profile/RefreshToken SQL has been issued, and prove retry creates exactly one account with one welcome grant.

## Guardrails

- Invoke the real HTTP endpoint.
- Use file-backed SQLite and fresh verification scopes.
- Inject failure from `SavedChangesAsync`, after SQL but before transaction commit.
- Assert no orphan Identity user, profile, refresh token, or doubled welcome coins.
- Assert internal failure details do not reach the client.

## Files inspected

- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `tests/MathLearning.Tests/Endpoints/AuthMobileRegistrationAtomicityTests.cs`
- `tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs`
- `tests/MathLearning.Tests/MathLearning.Tests.csproj`

## Files changed

- `tests/MathLearning.Tests/Endpoints/AuthMobileRegistrationRelationalAtomicityTests.cs`
- `docs/prompt_queues/backend_test_coverage.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-017-evidence.md`

## Implemented tests

1. Failure after Profile SQL is issued rolls back both Identity user and profile.
2. Failure after RefreshToken SQL is issued rolls back Identity user, profile, and token.
3. Retry after the relational token failure succeeds with exactly one Identity user, one profile, one refresh token, and exactly 100 welcome coins.

The injected secret is asserted absent from the public response; the endpoint must return the existing generic registration error.

## Why this is important

The previous validated tests exercised the non-relational compensating-cleanup path with EF InMemory. They did not prove the production-style transaction path rolls back SQL already written by Identity and later registration saves.

## Static validation

- The custom factory follows the existing `CustomWebApplicationFactory` service replacement pattern.
- Interceptor is disarmed during host creation and seed.
- Failure is targeted by entity type and thrown only once after successful `SaveChangesAsync` completion.
- Verification uses a fresh request scope and both `UserManager` and direct EF queries.
- Unique usernames isolate tests and retries.

## Executable validation not run

No .NET SDK/repository checkout is available in this connector environment. GitHub returned no combined status checks for the test commit.

Required command:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~AuthMobileRegistrationRelationalAtomicityTests"
```

Also rerun the existing fast cleanup tests:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~AuthMobileRegistrationAtomicityTests"
```

## Residual risk

- PostgreSQL transaction/Identity-store behavior still needs database-validation CI.
- New tests are committed but not executable-proof validated.

## Completion

88%

## Commit SHAs

- `19d53083c37878cf3d48b52be45eb2c426fd6578` — start evidence
- `1a4a8fe8b538d95311822452f4e5a82834bddc67` — relational registration tests
- `6e717512bbb4992c0a0ce792390c649ff40fc7bf` — queue registration

## Cross-repo sync

Not applicable; existing registration contract is unchanged.

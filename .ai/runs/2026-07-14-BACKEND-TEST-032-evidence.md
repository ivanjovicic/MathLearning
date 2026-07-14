# BACKEND-TEST-032 Evidence

Prompt ID: BACKEND-TEST-032
Queue: docs/prompt_queues/backend_test_followups_2026_07_03.md
Agent/tool: Codex CLI
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: Codex CLI
Run mode: implementation
Token budget: unknown-not-exposed
Actual context: low
Started from queue status: Prompt-ready
Local collision check: git status already dirty with unrelated user/agent changes; avoid-path remains MathLearning.Admin and unrelated backend in-flight files
Relevant prior mistakes read:
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-QUEUE-001
- BACKEND-MISTAKE-AUTH-001
How this run avoids prior mistakes:
- reuse the workflow PostgreSQL connection conventions instead of creating a second provider harness
- keep SQLite/InMemory tests intact and add explicit PostgreSQL authority tests instead of reinterpreting relational tests as provider proof
- record exact local PostgreSQL validation and keep any remaining matrix gaps explicit in the run log
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Files inspected

- `AGENTS.md`
- `docs/BUGFIX_PATTERN_GUARDRAILS.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/prompt_queues/backend_test_followups_2026_07_03.md`
- `.github/workflows/database-validation.yml`
- `tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs`
- `tests/MathLearning.Tests/Infrastructure/DatabaseSchemaValidationTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthRefreshRelationalConcurrencyTests.cs`
- `tests/MathLearning.Tests/Endpoints/OfflineBatchRelationalAtomicityTests.cs`
- `tests/MathLearning.Tests/Services/AdminQuestionValidationTests.cs`
- `.ai/runs/2026-07-13-BACKEND-MIGRATION-001-evidence.md`
- `.ai/runs/2026-07-13-BACKEND-LATEST-WORKFLOW-002-evidence.md`

## Files changed

- `.ai/runs/2026-07-14-BACKEND-TEST-032-evidence.md`
- `.github/workflows/database-validation.yml`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/prompt_queues/backend_test_followups_2026_07_03.md`
- `tests/MathLearning.Tests/Helpers/PostgresTestDatabase.cs`
- `tests/MathLearning.Tests/Helpers/PostgresWebApplicationFactory.cs`
- `tests/MathLearning.Tests/Infrastructure/PostgresProviderValidationTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthRefreshPostgresConcurrencyTests.cs`

## Commands run

- `rg -n "PostgreSQL|Npgsql|Testcontainers|provider-sensitive|BACKEND-TEST-032|migration-from-zero|startup guard" tests src docs .ai/runs`
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~PostgresProviderValidationTests|FullyQualifiedName~AuthRefreshPostgresConcurrencyTests"` with `POSTGRES_PROVIDER_TESTS_REQUIRED=1` (failed on PostgreSQL authentication)
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~PostgresProviderValidationTests|FullyQualifiedName~AuthRefreshPostgresConcurrencyTests"` with provider env unset (passed as gated no-op compile/execution check)
- `python scripts/validate_agent_evidence.py`
- `git diff --check -- .github/workflows/database-validation.yml tests/MathLearning.Tests/Helpers/PostgresTestDatabase.cs tests/MathLearning.Tests/Helpers/PostgresWebApplicationFactory.cs tests/MathLearning.Tests/Infrastructure/PostgresProviderValidationTests.cs tests/MathLearning.Tests/Endpoints/AuthRefreshPostgresConcurrencyTests.cs .ai/runs/2026-07-14-BACKEND-TEST-032-evidence.md`
- `git rev-parse HEAD`

## What was done

- Added a shared PostgreSQL test database helper that creates an isolated database, migrates `ApiDbContext`, seeds canonical test data, and drops the database afterward.
- Added a shared PostgreSQL-backed `WebApplicationFactory` that reuses the existing test auth/background-job test conventions while binding `ApiDbContext` to Npgsql.
- Added provider-authority tests for:
  - refresh-token persistence on PostgreSQL;
  - fresh-migrated PostgreSQL schema guard;
  - refresh-token rotation race through the HTTP endpoint on PostgreSQL.
- Wired the main `Database Validation` workflow to export `POSTGRES_PROVIDER_TESTS_REQUIRED=1` and `TEST_POSTGRES_MAINTENANCE_CONNECTION_STRING=...5432...postgres`, so CI uses the same PostgreSQL service for the new provider lane.
- Updated the queue rows so `BACKEND-TEST-032` now reflects implemented harness plus pending authoritative workflow validation instead of staying plain `Prompt-ready`.

## What was missed

- The broader provider matrix is not finished yet: serializable retry, named unique-constraint retry, XP `FOR UPDATE`, idempotency ledger concurrent insert/replay, offline answer unique constraints, and outbox claim/lease race still need explicit PostgreSQL authority tests.
- No successful local PostgreSQL provider execution was captured because the local maintenance credential for `localhost:5432` did not match the workflow default.

## Validation run

- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~PostgresProviderValidationTests|FullyQualifiedName~AuthRefreshPostgresConcurrencyTests"` with `POSTGRES_PROVIDER_TESTS_REQUIRED=1` — failed: `Npgsql.PostgresException 28P01 password authentication failed for user "postgres"` while creating the isolated PostgreSQL test database.
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~PostgresProviderValidationTests|FullyQualifiedName~AuthRefreshPostgresConcurrencyTests"` with provider env unset — passed (3 passed, 0 failed). This proves compile/test integration and gated behavior only; it is not provider proof.
- `git diff --check -- .github/workflows/database-validation.yml tests/MathLearning.Tests/Helpers/PostgresTestDatabase.cs tests/MathLearning.Tests/Helpers/PostgresWebApplicationFactory.cs tests/MathLearning.Tests/Infrastructure/PostgresProviderValidationTests.cs tests/MathLearning.Tests/Endpoints/AuthRefreshPostgresConcurrencyTests.cs .ai/runs/2026-07-14-BACKEND-TEST-032-evidence.md` — passed with LF-to-CRLF warnings only.

## Validation not run

- Successful authoritative PostgreSQL lane execution — not run to completion locally because the available `localhost:5432` PostgreSQL instance rejected the workflow default maintenance credential.
- CI workflow rerun after the workflow env update — not run from this thread.
- `python scripts/validate_agent_evidence.py` — ran and failed due pre-existing repository-wide queue/run-log evidence debt outside this prompt's scope.
- CI: No GitHub Actions evidence found via connector.

## Waste categories

- Local PostgreSQL credential mismatch on the workflow port.
- Repository-wide evidence debt unrelated to this prompt.

## Mistakes observed

- none

## Where time/context was wasted

- On the first explicit provider-lane test attempt that failed before the new PostgreSQL helper could execute any migrated-test logic.
- On repo-wide evidence validation output unrelated to `BACKEND-TEST-032`.

## Why waste happened

- The local machine has a PostgreSQL listener on `5432`, but it does not accept the workflow default `postgres/postgres` maintenance credential.
- The required evidence validator scans historical queue/run-log debt across the whole repository, not just files touched by this prompt.

## What the next agent should avoid

- Do not create a second PostgreSQL harness for BE-PERF or outbox/adaptive/practice prompts; reuse `PostgresTestDatabase` and `PostgresWebApplicationFactory`.
- Do not claim the current provider lane is closed until CI or a valid local maintenance credential executes the new PostgreSQL tests for real.
- Do not treat the gated local no-env pass as provider proof.

## Docs/rules updated to prevent repeat

- `docs/prompt_queues/backend_test_coverage.md`
- `docs/prompt_queues/backend_test_followups_2026_07_03.md`

## Queue updated

- `BACKEND-TEST-032` moved from `Prompt-ready` to `Implemented / Workflow validation needed` in the central and detailed backend test queues.

## New optimized prompt added

- none

## Follow-up prompt

- `BACKEND-LATEST-WORKFLOW-002` or a focused `BACKEND-TEST-032` validation rerun with a valid PostgreSQL maintenance credential

## Completion %

- 75%

## Residual risk

- The shared PostgreSQL lane is now wired, but the authoritative provider matrix is only partially implemented and still lacks a successful CI/local execution against known-good PostgreSQL credentials.

## Commit SHA

- `ae603c29afc9fa3cba19b5122c1f0cf0f67516c1` (current HEAD; no new commit created in this run)

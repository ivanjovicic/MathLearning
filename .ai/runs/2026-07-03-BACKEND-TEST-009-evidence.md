# BACKEND-TEST-009 Evidence

Prompt ID: BACKEND-TEST-009
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Model mode/settings: reasoning, repository-editing
Client/IDE: ChatGPT web
Run mode: relational tests
Token budget: medium
Actual context: shared and economy idempotency rollback atomicity plus deterministic duplicate-insert race recovery on SQLite
Started from queue status: Partial / Needs validation
Local collision check: current main inspected; existing constraint and direct service state-machine tests cover unique indexes and state transitions but not transactional rollback or deterministic race-recovery branches
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: evidence created before tests; concurrency is coordinated deterministically rather than timing-based; no PostgreSQL equivalence or passing test claim without executable evidence
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Backend regression guardrails

Historical bug class protected: `idempotency-offline-replay`, `schema-migration-drift`
Why this change can reintroduce it: InMemory tests do not prove unique constraints, transaction rollback, multi-context visibility, or duplicate-insert recovery
Files inspected: shared/economy idempotency services, direct service tests, SQLite constraint tests, ApiDbContext transaction patterns
Tests/validation planned: file-backed SQLite contexts, explicit transaction rollback, coordinated two-context inserts, replay verification after winning settlement
Contract/schema/docs touched: tests and coverage queue only
Residual risk if validation cannot run: SQLite compile/runtime behavior and provider-specific PostgreSQL concurrency remain unproven

## Files inspected

- `src/MathLearning.Infrastructure/Services/IdempotencyLedgerService.cs`
- `src/MathLearning.Infrastructure/Services/EconomyTransactionService.cs`
- `src/MathLearning.Application/Services/IIdempotencyLedgerService.cs`
- `src/MathLearning.Application/Services/IEconomyTransactionService.cs`
- `tests/MathLearning.Tests/Services/IdempotencyLedgerServiceTests.cs`
- `tests/MathLearning.Tests/Services/EconomyTransactionStateMachineTests.cs`
- `tests/MathLearning.Tests/Idempotency/RelationalIdempotencyConstraintTests.cs`

## Files changed

- `tests/MathLearning.Tests/Idempotency/RelationalIdempotencyTransactionTests.cs`
- `docs/prompt_queues/backend_test_coverage.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-009-evidence.md`

## Commands run

- GitHub repository search and direct file inspection
- GitHub fetch of committed test file for static source review
- GitHub combined-status lookup for commit `42dfb42f6a8f1d3d22b29a657db78a53c80157b0`

## What was done

- Added a relational rollback test proving shared-ledger completion and its associated domain mutation disappear together after transaction rollback.
- Added the equivalent economy transaction/balance rollback test.
- Added deterministic two-context shared-ledger duplicate-insert coverage.
- Added deterministic two-context economy duplicate-insert coverage.
- Used a `SaveChangesInterceptor` coordinator so both callers complete their initial lookup before either insert is allowed to persist.
- Allowed the first writer to commit before releasing the second writer, forcing the second service through its unique-constraint recovery and reload branch without sleep/timing assumptions.
- Completed the winning row from a fresh context and proved a later retry replays the single persisted completed result.
- Used file-backed SQLite with separate contexts, WAL mode, shared cache, disabled pooling, and a bounded busy timeout.
- Reconciled the test queue so already validated security/privacy/bounds packages are no longer presented as Ready work.

## What was missed

- No executable .NET test result was available in this connector environment.
- PostgreSQL serialization and provider-specific unique-violation behavior still need CI execution; SQLite is not claimed as equivalent proof.
- Coverage percentage was not produced.

## Validation run

- Static source-to-service contract review.
- Confirmed `IdempotencyLedgerBeginResult` and `EconomyTransactionBeginResult` properties used by tests match current interfaces.
- Confirmed each concurrent writer uses an independent `ApiDbContext` and relational database connection.
- Confirmed rollback verification uses a fresh context, preventing change-tracker state from producing a false pass.
- GitHub combined statuses for the test commit: none returned.

## Validation not run

- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~RelationalIdempotency"` — not run because the execution environment has no .NET SDK/repository checkout.
- full Release build — not run for the same reason.
- PostgreSQL schema-from-zero and concurrency validation — pending GitHub Actions/local execution.

## Waste categories

- connector execution limitation
- stale queue reconciliation

## Mistakes observed

- none

## Where time/context was wasted

- Several Ready queue rows were already covered and validated by earlier BACKEND-CRIT packages; their status had not been synchronized into the test queue.

## Why waste happened

- The test queue was created after some security packages had already landed and was not reconciled against their evidence logs.

## What the next agent should avoid

- Do not replace deterministic coordination with sleep-based concurrency tests.
- Do not claim SQLite proves PostgreSQL serialization behavior; keep PostgreSQL CI as the final relational authority.
- Do not add duplicate tests for BACKEND-TEST-004 through 008 or 010 without identifying a new branch or endpoint.
- Do not mark BACKEND-TEST-009 Done until the focused test filter passes.

## Docs/rules updated to prevent repeat

- Added a queue rule requiring reconciliation before adding new coverage.
- Recorded validated prior package counts and evidence paths.
- Documented the exact deterministic concurrency mechanism and PostgreSQL follow-up.

## Queue updated

- BACKEND-TEST-004: Validated — 25 passed.
- BACKEND-TEST-005: Validated — 41 passed; focused subset 6 passed.
- BACKEND-TEST-006: Validated — 9 passed.
- BACKEND-TEST-007: Validated — 10 passed.
- BACKEND-TEST-008: Validated — 43 passed.
- BACKEND-TEST-009: Implemented / Needs validation.
- BACKEND-TEST-010: Validated — 70 passed.
- BACKEND-TEST-012: Confirmed drift / Needs safe patch.

## New optimized prompt added

- none; BACKEND-TEST-012 and BACKEND-TEST-013 remain the next P0/P1 tasks.

## Follow-up prompt

- Run the focused relational test filter and Release build. If green, run the full database-validation workflow against PostgreSQL. Then apply BACKEND-TEST-012 locally as a targeted two-line EF model/snapshot patch and add its metadata/persistence regression tests.

## Completion %

- 88%

## Residual risk

- New relational tests are committed but not compiled or executed in this environment.
- SQLite exercises constraints, rollback, and multi-context race recovery, but PostgreSQL remains the authority for production concurrency semantics.
- Refresh-token model drift remains unresolved and may generate a future shrinking migration or reject generated tokens under stricter relational enforcement.

## Commit SHAs

- `b5c418b7c5d8ed4c3784e4d04618568bf65db507` — start relational evidence
- `42dfb42f6a8f1d3d22b29a657db78a53c80157b0` — relational rollback and deterministic race tests
- `a001698a1043c3477053c6d456c69d599072c62b` — queue reconciliation and coverage documentation

## Cross-repo sync

Cross-repo sync: not applicable; backend test-only work.
Mobile docs touched: none

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
- `tests/MathLearning.Tests/Services/IdempotencyLedgerServiceTests.cs`
- `tests/MathLearning.Tests/Services/EconomyTransactionStateMachineTests.cs`
- `tests/MathLearning.Tests/Idempotency/RelationalIdempotencyConstraintTests.cs`

## Files changed

- this run log

## Commands run

- GitHub repository search and direct file inspection

## What was done

- Designed deterministic two-context duplicate-insert coordination so both requests query before the first insert commits.
- Designed rollback tests that mutate domain state and complete idempotency state inside one relational transaction, then verify rollback from a fresh context.

## What was missed

- Implementation and executable validation are in progress.

## Validation run

- Static inspection only so far.

## Validation not run

- focused `dotnet test` not run yet; connector environment has no executable repository checkout.

## Waste categories

- none

## Mistakes observed

- none

## Where time/context was wasted

- none

## Why waste happened

- none

## What the next agent should avoid

- Do not replace deterministic coordination with sleep-based concurrency tests.
- Do not claim SQLite proves PostgreSQL serialization behavior; keep PostgreSQL CI as the final relational authority.

## Docs/rules updated to prevent repeat

- pending

## Queue updated

- pending

## New optimized prompt added

- none

## Follow-up prompt

- pending

## Completion %

- 15%

## Residual risk

- rollback and race-recovery branches are not yet protected by committed relational tests.

## Commit SHA

- pending

## Cross-repo sync

Cross-repo sync: not applicable; backend test-only work.
Mobile docs touched: none

# BACKEND-TEST-012 Evidence

Prompt ID: BACKEND-TEST-012
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Model mode/settings: reasoning, repository-editing
Client/IDE: ChatGPT web
Run mode: bugfix + tests
Token budget: medium
Actual context: refresh-token generator, EF model metadata, migration history, model snapshot, relational persistence safety
Started from queue status: Ready / P0-P1
Local collision check: current main and latest backend test queue inspected; BACKEND-TEST-014 landed separately and does not overlap this auth/schema task
Relevant prior mistakes read: BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: evidence created before runtime/test edits; existing migration is treated as source of intended schema truth; no passing build/test claim without executable evidence
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Backend regression guardrails

Historical bug class protected: `schema-migration-drift`, `auth-user-scope`, `compiler-warning-cleanliness`
Why this change can reintroduce it: changing only the generator, model, snapshot, or migration leaves schema-from-zero and production-upgraded databases inconsistent
Files inspected: refresh token service tests, EF model configuration, model snapshot, existing length migration, test DbContext factory, mistake ledger and coverage queue
Tests/validation planned: EF metadata regression test, generator-to-model fit assertion, SQLite relational persistence test, schema-from-zero validation command, focused auth test command
Contract/schema/docs touched: EF model and snapshot alignment only; no new migration because migration history already widened the column to 128
Residual risk if validation cannot run: compile, generated migration diff, and PostgreSQL schema-from-zero remain unproven until local or CI execution

## Files inspected

- `docs/prompt_queues/backend_test_coverage.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/ApiDbContextModelSnapshot.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/20260210114958_IncreaseRefreshTokenLength.cs`
- `src/MathLearning.Infrastructure/Services/RefreshTokenService.cs`
- `tests/MathLearning.Tests/Services/RefreshTokenServiceSecurityTests.cs`
- `tests/MathLearning.Tests/Helpers/TestDbContextFactory.cs`

## Files changed

- this run log

## Commands run

- GitHub repository search and direct file inspection

## What was done

- Confirmed generator emits Base64 for 64 random bytes (88 characters).
- Confirmed migration history widened `RefreshTokens.Token` to 128.
- Confirmed current EF model and snapshot still declare 64.

## What was missed

- Implementation and executable validation are in progress.

## Validation run

- Static inspection only so far.

## Validation not run

- `dotnet test` and schema-from-zero validation not run yet; connector environment has no executable repository checkout.

## Waste categories

- connector full-file edit limitation

## Mistakes observed

- BACKEND-MISTAKE-AUTH-001 confirmed as still open.

## Where time/context was wasted

- none

## Why waste happened

- none

## What the next agent should avoid

- Do not create a redundant migration that re-widens a column already widened by `20260210114958_IncreaseRefreshTokenLength`.

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

- Current EF model can reject generated tokens or produce a future shrinking migration.

## Commit SHA

- pending

## Cross-repo sync

Cross-repo sync: not applicable; backend persistence metadata only.
Mobile docs touched: none

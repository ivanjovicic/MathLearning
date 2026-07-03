# BACKEND-TEST-015 Evidence

Prompt ID: BACKEND-TEST-015
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Model mode/settings: reasoning, repository-editing
Client/IDE: ChatGPT web
Run mode: relational HTTP integration tests
Token budget: medium
Actual context: real `/auth/refresh` concurrent rotation against file-backed SQLite and EF concurrency-token SQL behavior
Started from queue status: new risk discovered during coverage reconciliation
Local collision check: existing auth refresh tests validated InMemory concurrency and sequential endpoint reuse; no relational HTTP refresh race test found
Relevant prior mistakes read: BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: invoke the real endpoint, coordinate both requests before database write without sleeps, use separate request scopes/DbContexts, and avoid claiming PostgreSQL equivalence
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Backend regression guardrails

Historical bug class protected: `auth-user-scope`, `schema-migration-drift`
Why this change can reintroduce it: EF InMemory concurrency tests do not prove relational update predicates, transaction rollback of the losing child token, or actual endpoint exception mapping
Files inspected: AuthEndpoints refresh handler, CustomWebApplicationFactory, existing auth refresh concurrency/regression tests, RefreshToken EF concurrency configuration
Tests/validation planned: real HTTP login, two coordinated refresh requests, one 200/one 401, exactly one active descendant and no third token
Contract/schema/docs touched: test-only plus queue/evidence
Residual risk if validation cannot run: SQLite test compile/runtime and provider-specific PostgreSQL concurrency remain unproven

## Files inspected

- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `tests/MathLearning.Tests/Endpoints/AuthRefreshConcurrencyTests.cs`
- `tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs`
- `.ai/runs/2026-07-01-BACKEND2-CRIT-002-evidence.md`

## Files changed

- this run log

## Commands run

- GitHub repository search and direct file inspection

## What was done

- Confirmed existing four auth tests pass but use EF InMemory for the concurrent save collision.
- Designed a relational endpoint test with deterministic SaveChanges coordination.

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

- Do not replace the coordinator with delays.
- Do not test only direct EF saves; keep the real HTTP endpoint and response mapping in scope.

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

- relational concurrent endpoint rotation is not yet protected by a committed test.

## Commit SHA

- pending

## Cross-repo sync

Cross-repo sync: not applicable; existing backend auth contract is unchanged.
Mobile docs touched: none

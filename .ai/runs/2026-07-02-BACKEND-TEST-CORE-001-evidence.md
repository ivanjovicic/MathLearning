# BACKEND-TEST-CORE-001 Evidence

Prompt ID: BACKEND-TEST-CORE-001
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Model mode/settings: reasoning, repository-editing
Client/IDE: ChatGPT web
Run mode: tests/implementation
Token budget: medium
Actual context: auth token primitives, economy idempotency, Daily Run/season/cosmetics settlement, relational constraints, CI coverage artifacts
Started from queue status: ad-hoc user request; queue created during run
Local collision check: GitHub main inspected; local worktree unavailable in connector environment
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: durable run log created before tests; no test/build success claimed without executable evidence
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Files inspected

- `AGENTS.md`
- `docs/AGENT_RUN_LOG_ENFORCEMENT.md`
- `.ai/RUN_LOG_TEMPLATE.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `docs/BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md`
- `docs/DOCS_INDEX.md`
- `.github/workflows/database-validation.yml`
- `tests/MathLearning.Tests/MathLearning.Tests.csproj`
- `tests/MathLearning.Tests/Helpers/TestDbContextFactory.cs`
- `tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs`
- existing Daily Run, economy, auth, user-scope, mobile contract, and SQLite concurrency tests
- `src/MathLearning.Api/Endpoints/DailyRunCosmeticsSettlement.cs`
- `src/MathLearning.Api/Endpoints/CosmeticsEndpoints.cs`
- `src/MathLearning.Api/Endpoints/CosmeticsEndpointHelpers.cs`
- `src/MathLearning.Infrastructure/Services/EconomyTransactionService.cs`
- `src/MathLearning.Infrastructure/Services/RefreshTokenService.cs`
- idempotency and Daily Run EF model configuration

## Files changed

- `src/MathLearning.Api/Properties/AssemblyInfo.cs`
- `tests/MathLearning.Tests/Endpoints/DailyRunCosmeticsSettlementTests.cs`
- `tests/MathLearning.Tests/Endpoints/DailyRunFragmentGrantTrustBoundaryTests.cs`
- `tests/MathLearning.Tests/Services/EconomyTransactionStateMachineTests.cs`
- `tests/MathLearning.Tests/Services/RefreshTokenServiceSecurityTests.cs`
- `tests/MathLearning.Tests/Idempotency/RelationalIdempotencyConstraintTests.cs`
- `tests/MathLearning.Tests/coverage.runsettings`
- `.github/workflows/database-validation.yml`
- `docs/BACKEND_TEST_COVERAGE_STRATEGY.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/DOCS_INDEX.md`
- this run log

## Commands run

- GitHub repository search/file inspection
- GitHub compare base `9aeaa9309f366f3a97e130ab6f3c9cb386138ab9` to `main`
- GitHub combined-status lookup for latest commits
- direct local clone attempted once and stopped after DNS failure

## What was done

- Added direct branch tests for Daily Run cosmetics settlement helper behavior.
- Added endpoint trust-boundary tests proving Daily Run fragment grants require the authenticated user's chest and season settlement.
- Added tests proving client-supplied fragment/copy values cannot override the stored server reward.
- Added replay test proving fragments are not granted twice.
- Added economy transaction state-machine tests for failed/completed transitions, replay, key conflicts, required values, and user/type isolation.
- Added refresh-token security tests for entropy size, uniqueness, metadata, expiry, validation, and idempotent revoke.
- Added SQLite relational tests for economy, Daily Run, and cosmetics unique constraints.
- Added Cobertura/JSON coverage settings and CI test/coverage artifact retention.
- Added risk-based coverage strategy and follow-up queue.

## What was missed

- No executable local `dotnet build` or `dotnet test` result was available in this environment.
- GitHub combined status returned no statuses for the latest commit.
- Push-triggered workflow runs are not exposed by the available commit workflow query.
- Coverage baseline and thresholds cannot be set honestly until the first artifact is produced.
- Remaining P0/P1 test packages are queued, not implemented in this run.

## Validation run

- Static code/model inspection against current implementations and existing test patterns.
- GitHub compare: branch is 12 commits ahead of the starting commit, with 5 new test files plus CI/docs support.
- GitHub combined status: no statuses returned for latest commit.

## Validation not run

- `dotnet build MathLearning.slnx` — not run; direct clone unavailable due container DNS/network restriction.
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj` — not run for the same reason.
- Coverage generation — pending next GitHub Actions or local .NET run.

## Waste categories

- environment/network limitation
- connector workflow-visibility limitation

## Mistakes observed

Mistakes observed: none

## Where time/context was wasted

- One local clone attempt failed because the execution environment could not resolve GitHub.
- Several searches confirmed existing coverage before new tests were selected; this avoided duplicate tests but increased inspection cost.

## Why waste happened

- Container network/DNS is unavailable for direct Git clone.
- Repository already has broad tests, so gap analysis was necessary before adding non-duplicative tests.

## What the next agent should avoid

- Do not retry local clone repeatedly in the same environment.
- Do not duplicate existing Daily Run claim/reward replay tests.
- Do not set arbitrary global coverage thresholds before measuring the first stable artifact.

## Docs/rules updated to prevent repeat

- Added `docs/BACKEND_TEST_COVERAGE_STRATEGY.md` with test-layer and coverage-gate rules.
- Updated `docs/DOCS_INDEX.md`.

## Queue updated

- Added `docs/prompt_queues/backend_test_coverage.md`.
- `BACKEND-TEST-CORE-001` is `Needs validation`.

## New optimized prompt added

- `BACKEND-TEST-002` — settlement snapshot truth: prove first response and exact replay include newly persisted season/milestone state.

## Follow-up prompt

- Run the Release build and the full test project with coverage settings. Fix only compile/test failures introduced by `BACKEND-TEST-CORE-001`, record TRX/Cobertura evidence, then change the queue row from `Needs validation` to a truthful completion status.

## Completion %

- 78%

## Residual risk

- The test design is broad and risk-focused, but compilation, runtime behavior, PostgreSQL CI, and coverage numbers remain unproven until executable validation runs.

## Cross-repo sync

- not applicable; this run adds backend tests for existing contracts and does not change mobile payload behavior.

## Commit SHAs

- `11213ffb9e1d6d081946186a1c2cf949e94fa180` — start run log
- `fe5d5d5cc4ece495667c3831fe90498f15412d25` — expose API internals to tests
- `a3e7c3bcf0abcaaa1b94397cd0cfc2989923c5db` — Daily Run settlement helper tests
- `ac5783437363399df3892fb06a1c5e510b9cab45` — economy state-machine tests
- `fade5b612afe568e8a565353ff3a63d9d7c23297` — refresh-token security tests
- `30e4df051438628b2061a66a0c2c6f981db47985` — fragment trust-boundary tests
- `0a25ebe9a2e701652a5706ba9c9a109c77783ac4` — coverage settings
- `5d6cf41c30f2bec90d34f78fa6866429d5c63ac3` — CI coverage artifacts
- `d05c881e3f8ec18a5659b17551aa59194f12b5dd` — coverage strategy
- `8b81327bce366c488196b48b3adc02cb5f6af236` — test coverage queue
- `680702a24ec0362b8ae8edab0fb8b46b4b9aace9` — relational constraint tests
- `547d9faf98045635325e5315d269f51a6ab6f929` — docs index

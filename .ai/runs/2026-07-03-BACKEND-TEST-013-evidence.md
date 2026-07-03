# BACKEND-TEST-013 Evidence

Prompt ID: BACKEND-TEST-013
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Model mode/settings: reasoning
Client/IDE: ChatGPT web
Run mode: tests
Token budget: medium
Actual context: focused idempotency service and canonicalization coverage
Started from queue status: new focused prompt derived from documented uncovered service-level idempotency requirements
Local collision check: connector-only run; latest main commit showed BACKEND-TEST-003 already claimed, so this run uses distinct prompt ID and owned paths
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: evidence file created before test changes; queue will remain Needs validation unless executable .NET evidence is available
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Backend regression guardrails

Historical bug class protected: `idempotency-offline-replay`
Why this change can reintroduce it: shared and cosmetics idempotency services own duplicate/conflict/state-transition behavior, but lacked direct service-level regression tests
Files inspected: idempotency service implementations, service interfaces, canonicalizer, existing economy state-machine tests, test DbContext factory, coverage strategy and queue
Tests/validation planned: focused service tests first; broader test project only when executable environment or CI evidence is available
Contract/schema/docs touched: tests and test coverage queue only; no endpoint contract or schema change planned
Residual risk if validation cannot run: compile/runtime behavior remains unverified until a .NET or GitHub Actions run executes the new tests

## Files inspected

- `src/MathLearning.Infrastructure/Services/IdempotencyLedgerService.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticsIdempotencyService.cs`
- `src/MathLearning.Infrastructure/Services/Idempotency/IdempotencyPayloadCanonicalizer.cs`
- `src/MathLearning.Application/Services/IIdempotencyLedgerService.cs`
- `src/MathLearning.Application/Services/ICosmeticsIdempotencyService.cs`
- `tests/MathLearning.Tests/Services/EconomyTransactionServiceTests.cs`
- `tests/MathLearning.Tests/Services/EconomyTransactionStateMachineTests.cs`
- `tests/MathLearning.Tests/Services/IdempotencyPayloadCanonicalizerTests.cs`
- `tests/MathLearning.Tests/Helpers/TestDbContextFactory.cs`
- `docs/BACKEND_TEST_COVERAGE_STRATEGY.md`
- `docs/backend_contract_gap_report.md`
- `docs/prompt_queues/backend_test_coverage.md`

## Files changed

- this run log

## Commands run

- GitHub connector repository/file inspection only

## What was done

- Confirmed direct service-level coverage is missing for shared and cosmetics idempotency ledgers.
- Confirmed existing season snapshot tests already cover the stale queue concern, avoiding duplicate tests.
- Reserved a distinct prompt after detecting active BACKEND-TEST-003 work.

## What was missed

- Test implementation and executable validation are still in progress.

## Validation run

- Static inspection of current service contracts and existing test conventions.

## Validation not run

- `dotnet test` not run yet; direct repository clone is unavailable in this environment.

## Waste categories

- environment/network limitation
- parallel-agent collision avoided

## Mistakes observed

Mistakes observed: none

## Where time/context was wasted

- Existing endpoint coverage and queue freshness had to be checked before selecting a non-duplicative test package.

## Why waste happened

- The repository recently received a broad test batch and one queue item was already covered despite remaining Ready.

## What the next agent should avoid

- Do not duplicate season snapshot tests already present in `EconomySettlementEndpointsIntegrationTests.cs`.
- Do not touch BACKEND-TEST-003 owned paths while that prompt is active.

## Docs/rules updated to prevent repeat

- Pending queue update after implementation.

## Queue updated

- Pending.

## New optimized prompt added

- BACKEND-TEST-013: direct shared/cosmetics idempotency state-machine and canonical payload tests.

## Follow-up prompt

- Add relational concurrent duplicate settlement coverage after these deterministic service tests are executable and stable.

## Completion %

- 15%

## Residual risk

- New tests are not yet implemented or executed.

## Cross-repo sync

Cross-repo sync: not applicable
Mobile docs touched: none

## Commit SHA

- pending

# BACKEND-TEST-003 Evidence

Prompt ID: BACKEND-TEST-003
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Model mode/settings: reasoning, repository-editing
Client/IDE: ChatGPT web
Run mode: tests/investigation
Token budget: medium
Actual context: P0 operation identity resolution for quiz answer, SRS update, and offline batch replay
Started from queue status: Ready
Local collision check: current `main` files and latest backend test commits inspected; local worktree unavailable in connector environment
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes: create evidence before test commits; do not claim executable validation without a test run; record cross-repo impact explicitly
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Backend regression guardrails

Historical bug class protected: `idempotency-offline-replay`, `mobile-contract-shape`
Why this change can reintroduce it: tests that assume both operation keys are always supplied can miss legacy/single-key replay behavior and allow retries to mutate twice
Files inspected: quiz/SRS endpoint helpers and endpoints, offline batch processing, existing idempotency and contract tests, mobile idempotency handoff
Tests/validation planned: focused helper tests plus HTTP integration tests for single-key and missing-key behavior; targeted `dotnet test` filter when executable validation is available
Contract/schema/docs touched: tests and backend test queue only; no endpoint payload or schema change planned
Residual risk if validation cannot run: compile/runtime behavior remains unproven until local or CI .NET execution

## Files inspected

- `AGENTS.md`
- `docs/AGENT_QUICKSTART.md`
- `docs/BACKEND_REGRESSION_GUARDRAILS.md`
- `docs/mobile_contract_idempotency_handoff.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `src/MathLearning.Api/Endpoints/QuizEndpointHelpers.cs`
- `src/MathLearning.Api/Endpoints/SrsEndpointHelpers.cs`
- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- `src/MathLearning.Api/Endpoints/SrsEndpoints.cs`
- `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs`
- `tests/MathLearning.Tests/Idempotency/SrsUpdateIdempotencyTests.cs`
- `tests/MathLearning.Tests/Endpoints/OfflineBatchSubmitTests.cs`

## Files changed

- this run log

## Commands run

- GitHub repository search and direct file inspection

## What was done

- Confirmed existing season settlement snapshot coverage before avoiding duplicate tests.
- Confirmed quiz and SRS accept either one stable key and copy it into both ledger dimensions.
- Confirmed missing keys deliberately enter legacy non-ledger paths.
- Confirmed offline replay uses stable session identity when supplied and answer timestamp deduplication as the final duplicate guard.

## What was missed

- Implementation and executable validation are still in progress.

## Validation run

- Static inspection only so far.

## Validation not run

- `dotnet test` not run yet; connector environment has no executable repository checkout.

## Waste categories

- stale queue discovery
- duplicate-test avoidance
- connector environment limitation

## Mistakes observed

- none so far

## Where time/context was wasted

- BACKEND-TEST-002 was still marked Ready even though equivalent endpoint and mobile-contract assertions already exist.

## Why waste happened

- Queue status was not reconciled after later season test improvements.

## What the next agent should avoid

- Do not add another season snapshot test unless it covers a distinct transactional or concurrency invariant.

## Docs/rules updated to prevent repeat

- pending queue reconciliation

## Queue updated

- pending

## New optimized prompt added

- none

## Follow-up prompt

- pending after operation-identity test results

## Completion %

- 20%

## Residual risk

- P0 single-key and missing-key behavior is not yet protected by focused regression tests.

## Commit SHA

- pending

## Cross-repo sync

Cross-repo sync: not applicable; tests document existing backend compatibility behavior and do not change mobile payload behavior.
Mobile docs touched: none

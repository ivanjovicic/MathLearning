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
How this run avoids prior mistakes: evidence created before test commits; no executable validation claimed without a test run; cross-repo impact recorded explicitly
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Backend regression guardrails

Historical bug class protected: `idempotency-offline-replay`, `mobile-contract-shape`
Why this change can reintroduce it: tests that assume both operation keys are always supplied can miss legacy/single-key replay behavior and allow retries to mutate twice
Files inspected: quiz/SRS endpoint helpers and endpoints, offline batch processing, existing idempotency and contract tests, mobile idempotency handoff
Tests/validation planned: focused helper tests plus HTTP integration tests for single-key and missing-key behavior; targeted `dotnet test` filter when executable validation is available
Contract/schema/docs touched: tests and backend test queue only; no endpoint payload or schema change
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
- `src/MathLearning.Api/Endpoints/ApiDbTransactionHelpers.cs`
- `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs`
- `tests/MathLearning.Tests/Idempotency/SrsUpdateIdempotencyTests.cs`
- `tests/MathLearning.Tests/Endpoints/OfflineBatchSubmitTests.cs`
- `tests/MathLearning.Tests/Endpoints/OfflineBatchSubmitCompatibilityTests.cs`
- `tests/MathLearning.Tests/Endpoints/EconomySettlementEndpointsIntegrationTests.cs`
- `tests/MathLearning.Tests/Contracts/MobileEconomyContractIntegrationTests.cs`

## Files changed

- `tests/MathLearning.Tests/Endpoints/OperationIdentityResolutionTests.cs`
- `tests/MathLearning.Tests/Contracts/OperationIdentityContractIntegrationTests.cs`
- `docs/prompt_queues/backend_test_coverage.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-003-evidence.md`

## Commands run

- GitHub repository search and direct file inspection
- GitHub fetch of newly committed test files for static review
- GitHub workflow-run lookup for commit `4e9476c3e4712136ea33edc1acff5121bc635d74`
- GitHub combined-status lookup for commit `4e9476c3e4712136ea33edc1acff5121bc635d74`
- `dotnet --info` probe in execution environment

## What was done

- Confirmed existing season settlement snapshot coverage and changed BACKEND-TEST-002 to `Covered / Needs validation` instead of adding duplicate tests.
- Added 14 focused helper test cases for quiz/SRS missing, whitespace, single-key, and distinct-key resolution.
- Added 7 HTTP/integration test cases for quiz answer, SRS update, and offline submit operation identity behavior.
- Proved through test assertions that a lone `operationId` or `idempotencyKey` is promoted to both ledger dimensions.
- Proved exact single-key retries replay the settled result and do not mutate quiz/SRS state twice.
- Characterized current legacy behavior: missing quiz/SRS identity bypasses the idempotency ledger.
- Characterized current offline behavior: empty session identity still deduplicates the answer/XP mutation by timestamp, but creates a new quiz-session row for each replay.
- Added BACKEND-TEST-013 to force an explicit strict-vs-legacy compatibility decision rather than leaving missing identity implicit.

## What was missed

- No executable .NET build/test result was available in this environment.
- No coverage percentage was produced; global thresholds remain intentionally deferred until a real artifact exists.
- BACKEND-TEST-013 changes runtime policy and was queued rather than implemented in this test-only prompt.

## Validation run

- Static source-to-test review against current helper and endpoint branches.
- Verified new tests use the repository's existing `CustomWebApplicationFactory`, auth header, user seeding, and EF assertion patterns.
- Verified all new paths exist on `main` after commit.
- GitHub workflow runs for the checked commit: none returned.
- GitHub combined statuses for the checked commit: none returned.

## Validation not run

- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~OperationIdentity"` — not run because the execution environment has no .NET SDK/repository checkout.
- `dotnet build MathLearning.slnx -c Release` — not run for the same reason.
- Coverage collection — not run; requires executable test infrastructure.

## Waste categories

- stale queue discovery
- duplicate-test avoidance
- connector environment limitation

## Mistakes observed

- none; the missing-identity behavior is a product compatibility risk, not an agent mistake

## Where time/context was wasted

- BACKEND-TEST-002 was still marked Ready even though equivalent endpoint and mobile-contract assertions already existed.
- Existing offline compatibility tests had to be inspected before adding a non-duplicative empty-session characterization.

## Why waste happened

- Queue status was not reconciled after later season test improvements.
- The repository already has broad idempotency coverage, so gap selection required branch-level comparison.

## What the next agent should avoid

- Do not add another season snapshot test unless it covers a distinct transactional or concurrency invariant.
- Do not treat missing operation identity as safely idempotent merely because one offline duplicate guard exists.
- Do not mark BACKEND-TEST-003 Done until the targeted test filter runs successfully.

## Docs/rules updated to prevent repeat

- Reconciled BACKEND-TEST-002 status with existing evidence.
- Expanded BACKEND-TEST-003 with exact implemented test scope and validation command.
- Added BACKEND-TEST-013 for the unresolved runtime contract decision.

## Queue updated

- BACKEND-TEST-002: `Covered / Needs validation`.
- BACKEND-TEST-003: `Implemented / Needs validation`.
- BACKEND-TEST-013: `Ready / P0 decision`.

## New optimized prompt added

- BACKEND-TEST-013 — enforce or explicitly bound missing operation identity across quiz/SRS/offline mutations.

## Follow-up prompt

- Run the targeted OperationIdentity tests and Release build. Fix only failures introduced by this batch, then choose and implement the BACKEND-TEST-013 compatibility policy with backend/mobile contract synchronization.

## Completion %

- 85%

## Residual risk

- New tests are committed but not compiled or executed in this environment.
- Canonical quiz/SRS calls without identity still bypass the ledger until BACKEND-TEST-013 is resolved.
- Empty offline session IDs can create unbounded quiz-session rows even though answer mutation dedupe succeeds.

## Commit SHAs

- `152196b6f9f75072b0e66eba4f3a981fc841cf7e` — start run evidence
- `5968cdf7e0d687a427bb1bc90c1bc53778aa50c4` — helper branch tests
- `8cff92100884fec30878cc24ba70d7b2a69b35c0` — HTTP operation identity contract tests
- `4e9476c3e4712136ea33edc1acff5121bc635d74` — queue reconciliation and follow-up

## Cross-repo sync

Cross-repo sync: not applicable; tests document existing backend compatibility behavior and do not change mobile payload behavior.
Mobile docs touched: none

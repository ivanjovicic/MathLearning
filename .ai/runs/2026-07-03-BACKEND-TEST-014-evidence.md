# BACKEND-TEST-014 Evidence

Prompt ID: BACKEND-TEST-014
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
Local collision check: connector-only run; BACKEND-TEST-003 and then BACKEND-TEST-013 were claimed by a parallel agent, so this package was moved to BACKEND-TEST-014 with distinct owned test paths
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: evidence exists with the test commits; queue remains Needs validation because no executable .NET evidence is available
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Backend regression guardrails

Historical bug class protected: `idempotency-offline-replay`
Why this change can reintroduce it: shared and cosmetics idempotency services own duplicate/conflict/state-transition behavior, but lacked direct service-level regression tests
Files inspected: idempotency service implementations, service interfaces, canonicalizer, existing economy state-machine tests, test DbContext factory, coverage strategy and queue
Tests/validation planned: focused service tests first; broader test project only when executable environment or CI evidence is available
Contract/schema/docs touched: tests and test coverage queue only; no endpoint contract or schema change
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

- `tests/MathLearning.Tests/Services/IdempotencyLedgerServiceTests.cs`
- `tests/MathLearning.Tests/Services/CosmeticsIdempotencyServiceTests.cs`
- `tests/MathLearning.Tests/Services/IdempotencyPayloadCanonicalizerTests.cs`
- `docs/prompt_queues/backend_test_coverage.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-014-evidence.md`

## Commands run

- GitHub connector source/test inspection
- GitHub combined status lookup for commit `e25dd16761953fe8476cb76837949ed5b695af9a`
- GitHub workflow-run lookup for the same commit

## What was done

- Added 11 direct shared-ledger state-machine tests.
- Added 12 direct cosmetics-ledger state-machine tests.
- Expanded canonical JSON/hash coverage from 3 to 10 tests, adding 7 scenarios.
- Added 30 new test scenarios total.
- Covered first processing, completed replay, failed replay, equivalent payload ordering, payload conflict, operation/key collisions, user and operation-type isolation where applicable, illegal transitions, unknown ledger IDs, required scope values, array-order significance, naming policy, `JsonElement`, null/primitives and SHA-256 stability.
- Confirmed existing season settlement snapshot tests already cover the stale queue concern, avoiding duplicate tests.
- Added BACKEND-TEST-014 to the coverage queue with an exact focused validation command and relational-boundary warning.

## What was missed

- True concurrent duplicate settlement against a relational provider remains under BACKEND-TEST-009.
- No executable local or CI test result was available through the current environment.

## Validation run

- Static inspection against current interfaces, implementations, entity properties, test project settings and existing test conventions.
- GitHub combined status returned no status checks for the latest test commit.
- GitHub commit workflow lookup returned no runs.

## Validation not run

- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~IdempotencyLedgerServiceTests|FullyQualifiedName~CosmeticsIdempotencyServiceTests|FullyQualifiedName~IdempotencyPayloadCanonicalizerTests"` — not run because direct repository clone/build is unavailable in this environment.
- No GitHub Actions evidence found via connector.

## Waste categories

- environment/network limitation
- parallel-agent prompt-ID collision corrected

## Mistakes observed

Mistakes observed: none

## Where time/context was wasted

- Existing endpoint coverage and queue freshness had to be checked before selecting a non-duplicative package.
- BACKEND-TEST-013 was claimed by a parallel agent after this run initially reserved it, requiring evidence renaming to BACKEND-TEST-014.

## Why waste happened

- Several agents were writing directly to the shared main branch and queue during the same period.

## What the next agent should avoid

- Do not duplicate season snapshot tests already present in `EconomySettlementEndpointsIntegrationTests.cs`.
- Do not treat EF InMemory tests as proof of unique indexes or true concurrent settlement.
- Run the focused command before marking BACKEND-TEST-014 Done.

## Docs/rules updated to prevent repeat

- Queue entry documents exact coverage and the remaining relational boundary.

## Queue updated

- Added BACKEND-TEST-014 as `Implemented / Needs validation`.
- Preserved the parallel agent's BACKEND-TEST-003 and BACKEND-TEST-013 entries.

## New optimized prompt added

- BACKEND-TEST-014: direct shared/cosmetics idempotency state-machine and canonical payload tests.

## Follow-up prompt

- Execute the focused test filter and repair only compile/runtime failures introduced by BACKEND-TEST-014; then continue BACKEND-TEST-009 with relational concurrent duplicate settlement.

## Completion %

- 85% (implementation complete; executable validation unavailable)

## Residual risk

- Tests are designed against current contracts but remain uncompiled/unexecuted in this environment.
- Race recovery after a real relational unique-index collision still lacks direct concurrent proof.

## Cross-repo sync

Cross-repo sync: not applicable
Mobile docs touched: none

## Commit SHA

- `f34daeca40f0070f28b72e287564745c327d03c8` — shared ledger service tests
- `ca9fa2133d51490f71d2aa7e707091bb9de75ddc` — cosmetics ledger service tests
- `e25dd16761953fe8476cb76837949ed5b695af9a` — canonical payload/hash tests
- `1b8d7d669069c8f070f82def634ff53d00981ee3` — corrected BACKEND-TEST-014 evidence path
- `383fd4f6a89dc70e1407de5c9d268bf1f053b842` — removed superseded BACKEND-TEST-013 evidence path
- `3e0a06ceae42d9dd9f386fa826a9610f368fd4eb` — queue update
- final evidence commit: this update

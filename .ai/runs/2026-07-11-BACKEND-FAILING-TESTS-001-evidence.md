# BACKEND-FAILING-TESTS-001 Evidence

Prompt ID: BACKEND-FAILING-TESTS-001
Queue: `docs/prompt_queues/backend_test_coverage.md`, `docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md`, and `docs/prompt_queues/backend_failing_test_followups_2026_07_11.md`
Agent/tool: ChatGPT via GitHub connector and GitHub Actions
Model provider: OpenAI
Model name/id: GPT-5.6 Thinking
Model mode/settings: reasoning, repository editing
Client/IDE: ChatGPT connector session
Run mode: validation-first, minimal repair
Token budget: high
Started from queue status: ad-hoc user request following BACKEND-COVERAGE-EXPANSION-001
Local collision check: active central/latest/API-DB queues were inspected before allocating this run ID and `BACKEND-MIGRATION-001`
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-VALIDATION-002, BACKEND-MISTAKE-QUEUE-001
How this run avoids prior mistakes: evidence created before edits; every failure classified from retained TRX/log evidence; repairs kept minimal and provider-aware; canonical public error text reused from source constants; unsafe migration work routed to a dedicated queue prompt; no full-suite-green claim was made until a checked 995/995 run existed
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Goal

Inspect all 35 backend test failures from the previous coverage run, identify the actual failure classes, repair what could be proved safely, route broader migration work into the queue, and publish the validated result to `main`.

## Starting evidence

- Starting branch: `main`
- Starting `main` SHA: `4a2593dba6514e4650622d5713771c68ac427784`
- Working branch: `agent/fix-failing-tests-2026-07-11`
- Source artifact: GitHub Actions diagnostic run `29145382740`, artifact `8246596984`
- Starting full-suite result: **960 passed, 35 failed, 995 total**
- Starting failure signatures:
  - 30 relational SQLite setup failures, initially `SQLite Error 1: near "AT": syntax error`;
  - 2 stale generic safe-error expectations;
  - 1 AdminApiClient actionable-message assertion;
  - 1 idempotency observability route-metadata assertion;
  - 1 StepEngine linear-equation result assertion.

## Root-cause classification

### 1. Provider-specific EF model leakage into SQLite

The 30 relational failures were one layered provider problem rather than 30 independent test defects:

1. `QuestionStat.NextReview` used PostgreSQL-only default SQL `NOW() AT TIME ZONE 'UTC'` for every provider.
2. After that was corrected, seven tests exposed PostgreSQL system column `xmin` being mapped into SQLite as a required row-version column.
3. After `xmin` was limited to Npgsql, two offline-batch tests exposed `SELECT ... FOR UPDATE` being used for every relational provider even though SQLite does not support that syntax.

The final repair preserves PostgreSQL behavior while giving SQLite a valid equivalent model/query path.

### 2. AdminApiClient relative URI bug

`Uri.TryCreate("/health", UriKind.Absolute, ...)` can produce an absolute `file:` URI. The prior guard therefore treated `/health` as already absolute and skipped the missing-`BaseAddress` diagnostic. Only absolute HTTP/HTTPS request URIs are now exempt from the base-address requirement.

### 3. StepEngine regex precedence bug

The simple `x + B = C` regex ran before `Ax + B = C` and partially matched `2x + 4 = 18` as `x + 4 = 18`, yielding `x = 14`. Complex coefficient equations are now matched first, yielding `x = 7`.

### 4. Stale safe-error assertions

Two endpoint tests asserted literal `An unexpected error occurred.` while the canonical public 500 contract is `SafeClientErrorResponse.GenericInternalError` (`Internal server error.`). Tests now reference the canonical constant rather than duplicating stale text.

### 5. Brittle route metadata lookup

The idempotency observability test compared the route raw text without normalizing the framework-provided trailing slash. The route and authorization policy were correct; the test now compares `TrimEnd('/')`.

### 6. Stale provider contract test

The former `ApiDbContext_MapsXminAsConcurrencyToken` test created an InMemory context but expected PostgreSQL `xmin`. It is now split into two explicit tests:

- non-Npgsql ApiDbContext does not map `xmin`;
- Npgsql ApiDbContext maps `xmin` as `ValueGenerated.OnAddOrUpdate` concurrency token.

## Files inspected

- prior TRX/test logs and coverage artifacts from run `29145382740`;
- `src/MathLearning.Admin/Services/AdminApiClient.cs`;
- `src/MathLearning.Application/Helpers/StepEngine.cs`;
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`;
- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`;
- `src/MathLearning.Api/Endpoints/ApiDbTransactionHelpers.cs`;
- `src/MathLearning.Infrastructure/Services/XpTrackingService.cs`;
- `src/MathLearning.Api/Middleware/SafeClientErrorResponse.cs`;
- all failing test files and their relational factories/interceptors;
- cosmetics migration chain and standard Database Validation workflow for residual classification;
- active backend queues before publishing a new prompt.

## Files changed

### Runtime/provider repairs

- `src/MathLearning.Admin/Services/AdminApiClient.cs`
- `src/MathLearning.Application/Helpers/StepEngine.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`

### Test contract repairs

- `tests/MathLearning.Tests/Endpoints/AnalyticsEndpointContractTests.cs`
- `tests/MathLearning.Tests/Endpoints/ExplanationEndpointContractTests.cs`
- `tests/MathLearning.Tests/Endpoints/IdempotencyObservabilityAuthorizationTests.cs`
- `tests/MathLearning.Tests/Services/AdminQuestionValidationTests.cs`

### Queue/evidence

- `docs/prompt_queues/backend_failing_test_followups_2026_07_11.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `.ai/runs/2026-07-11-BACKEND-FAILING-TESTS-001-evidence.md`

A temporary branch-only workflow, `.github/workflows/apply-failing-test-repairs.yml`, was used to apply and validate the patch in GitHub Actions and was deleted before merge.

## Validation progression

The iterative runs were retained because each run exposed the next provider layer:

- run `29149157453`, artifact `8247652200`: **65/72 passed**, seven `Questions.xmin` SQLite failures;
- run `29149276258`, artifact `8247686067`: **70/72 passed**, two SQLite `FOR UPDATE` offline-batch failures;
- run `29149413427`, artifact `8247733504`: focused **72/72 passed**, full suite **994/995 passed**, one stale InMemory/Npgsql `xmin` assertion;
- final run `29149563136`, artifact `8247778512`, validated head `63f97bbf11b142e58d0e33742dffed69b9df3ddc`:
  - restore: passed;
  - Release build: passed;
  - focused previously failing groups: **72 passed, 0 failed**;
  - complete test project: **995 passed, 0 failed, 0 skipped**;
  - validated runtime/test changes committed by workflow as `f4f8fd175f5beb9d7e1eb5b6f74376401f5831c6`.

### Commands represented by the final run

```text
dotnet restore MathLearning.slnx
dotnet build MathLearning.slnx -c Release --no-restore
```

Focused failure groups:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj \
  -c Release --no-build \
  --filter "FullyQualifiedName~AdminApiClientTests|FullyQualifiedName~AnalyticsEndpointContractTests|FullyQualifiedName~ExplanationEndpointContractTests|FullyQualifiedName~IdempotencyObservabilityAuthorizationTests|FullyQualifiedName~StepEngineTests|FullyQualifiedName~ApiDbTransactionHelpersRelationalTests|FullyQualifiedName~RelationalIdempotencyConstraintTests|FullyQualifiedName~RelationalIdempotencyTransactionTests|FullyQualifiedName~AuthRefreshRelationalConcurrencyTests|FullyQualifiedName~AuthMobileRegistrationRelationalAtomicityTests|FullyQualifiedName~OfflineBatchRelationalAtomicityTests|FullyQualifiedName~QuizAttemptIngestServiceRelationalTests|FullyQualifiedName~XpTrackingConcurrencyIntegrationTests"
```

Full suite:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj \
  -c Release --no-build \
  --logger "trx;LogFileName=mathlearning-tests.trx" \
  --results-directory artifacts/test-results
```

## What was done

- Reproduced and grouped every original failure by executable signature.
- Fixed two genuine runtime bugs: AdminApiClient URI classification and StepEngine regex precedence.
- Made ApiDbContext defaults and `xmin` mapping provider-aware.
- Preserved Npgsql row locking while avoiding unsupported SQLite `FOR UPDATE` syntax under the existing serializable transaction.
- Replaced stale literal safe-error assertions with the canonical source constant.
- Made route metadata comparison robust to a trailing slash without weakening the authorization-policy assertion.
- Split the `xmin` model test into explicit Npgsql and non-Npgsql contracts.
- Re-ran the exact affected groups and then the entire backend test project.
- Added and indexed `BACKEND-MIGRATION-001` for the independent schema-from-zero blocker.
- Removed the temporary patch workflow before merge.

## What was missed / intentionally deferred

The standard `Database Validation` workflow is not green because it still stops in the clean PostgreSQL migration chain before its normal test step. This is independent of the now-green 995-test project.

Confirmed residual:

- `20260309091241_AddCosmeticSystem` creates cosmetics/avatar foreign keys through raw PostgreSQL SQL with provider-generated names;
- `20260624133144_AlignCosmeticsMobileDataModel` attempts to drop hard-coded EF-style names;
- a clean database fails with PostgreSQL `42704` at `FK_user_avatar_configs_UserProfiles_UserId`.

This historical migration repair was not mixed into the test-repair patch because it requires proof for both clean and already-upgraded databases. It is fully specified as `BACKEND-MIGRATION-001` in `docs/prompt_queues/backend_failing_test_followups_2026_07_11.md` and linked from the central queue.

## Mistakes observed

- BACKEND-MISTAKE-VALIDATION-001 — the committed relational tests had not been executed against the current provider-aware model before the previous coverage claim.
- BACKEND-MISTAKE-VALIDATION-002 — `Database.IsRelational()` was incorrectly treated as proof that PostgreSQL-only SQL (`AT TIME ZONE`, `xmin`, `FOR UPDATE`) was valid for SQLite.
- BACKEND-MISTAKE-EVIDENCE-001 — each repair layer required retained TRX/log artifacts; static inspection alone would have stopped after the first symptom.
- BACKEND-MISTAKE-QUEUE-001 — the migration residual was checked against active queues before reserving a new canonical ID.

## Waste categories

- connector-only repository access with no local `gh`/checkout execution;
- standard workflow serially blocks tests behind schema-from-zero;
- the original provider leak produced cascading symptoms, requiring multiple runs to expose the next layer;
- temporary workflow was required to apply a multi-file patch and retain full untruncated test logs.

## Where time/context was wasted

- Initial standard logs truncated before the actionable stack traces.
- Fixing only `AT TIME ZONE` revealed `xmin`; fixing `xmin` revealed `FOR UPDATE`.
- The last stale `xmin` assertion mixed InMemory and PostgreSQL expectations, requiring one final full-suite run.

## Why waste happened

- Provider-specific behavior was keyed from broad `IsRelational()` checks or unconditionally modeled SQL rather than explicit provider capabilities.
- The standard CI workflow does not continue to tests when schema validation fails.

## What the next agent should avoid

- Do not reintroduce PostgreSQL-only SQL based solely on `Database.IsRelational()`.
- Do not remove Npgsql `xmin` or `FOR UPDATE` semantics merely to make SQLite tests pass.
- Do not duplicate `BACKEND-TEST-032`; it remains the canonical broader PostgreSQL provider lane.
- Do not weaken the schema gate to hide the cosmetics migration defect.
- Do not add a later migration that clean databases cannot reach.

## Queue updated

- Added `docs/prompt_queues/backend_failing_test_followups_2026_07_11.md`.
- Added and reserved `BACKEND-MIGRATION-001` as the canonical P0 migration repair.
- Indexed the queue and prompt in `docs/prompt_queues/backend_test_coverage.md`.
- Linked `BACKEND-MIGRATION-001` to existing `BACKEND-TEST-032` rather than duplicating the general PostgreSQL provider lane.

## Follow-up prompt

- `BACKEND-MIGRATION-001` — repair the cosmetics FK-name drift with clean PostgreSQL, upgraded-path, idempotent-script and startup-smoke evidence.

## Completion %

- **96%**

The requested failing-test repair is complete and the backend test project is fully green. Completion is capped because the independent standard migration/schema workflow remains red and is queued rather than repaired in this run.

## Residual risk

- Provider-aware branches are validated against the existing SQLite/InMemory/Npgsql model tests, but `BACKEND-TEST-032` remains necessary for a broader authoritative PostgreSQL concurrency/locking lane.
- Historical migration edits carry deployed-database risk and must follow `BACKEND-MIGRATION-001` dual-path validation.

## Commit SHAs

- evidence start: `acbb7ea40c7fbfa03725662301f76879a8737373`;
- provider/runtime/test repair commit: `f4f8fd175f5beb9d7e1eb5b6f74376401f5831c6`;
- queue prompt creation: `615706a5e602da75a513d63549ff4eaf557dd489`;
- temporary workflow removal: `b63ff6288facd527680fa70c7cfc12569c8d95e2`;
- central queue index update: `d9d3348467381b9bcd4e460c536fdedafd8eac87`;
- final merge SHA: pending.

## Cross-repo sync

Cross-repo impact: none. No mobile-facing request/response payload, error code, retry/conflict behavior or persistence contract changed.
Other repos checked: not required for this provider/test-only boundary.
Other repo docs touched: none.
Deferred sync reason: not applicable.
Follow-up prompt: backend-only `BACKEND-MIGRATION-001`.

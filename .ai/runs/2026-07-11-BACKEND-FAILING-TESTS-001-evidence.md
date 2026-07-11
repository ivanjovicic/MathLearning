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
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded
Commit SHA: `6cdff4c7fbeb595ed29fc11b4641d7b9fe488100`
Started from queue status: ad-hoc user request following BACKEND-COVERAGE-EXPANSION-001
Local collision check: active central/latest/API-DB queues were inspected before allocating this run ID and `BACKEND-MIGRATION-001`
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-VALIDATION-002, BACKEND-MISTAKE-QUEUE-001
How this run avoids prior mistakes: evidence created before edits; every failure classified from retained TRX/log evidence; repairs kept minimal and provider-aware; canonical public error text reused from source constants; unsafe migration work routed to a dedicated queue prompt; no full-suite-green claim was made until checked 995/995 and 996/996 runs existed

## Goal

Inspect all 35 backend test failures from the previous coverage run, repair what could be proved safely, route broader migration work into the queue, validate the final pull-request head, and publish the result to `main`.

## Starting evidence

- Starting `main` SHA: `4a2593dba6514e4650622d5713771c68ac427784`
- Working branch: `agent/fix-failing-tests-2026-07-11`
- Source diagnostic run: `29145382740`
- Source artifact: `8246596984`
- Starting result: **960 passed, 35 failed, 995 total**
- Starting failure groups:
  - 30 relational SQLite/provider failures;
  - 2 stale generic safe-error expectations;
  - 1 AdminApiClient actionable-message assertion;
  - 1 idempotency observability route-metadata assertion;
  - 1 StepEngine linear-equation assertion.

## Root causes and repairs

### Provider-specific EF and SQL behavior

The 30 relational failures were cascading symptoms of PostgreSQL behavior leaking into SQLite:

1. `QuestionStat.NextReview` used PostgreSQL-only `NOW() AT TIME ZONE 'UTC'` for every provider.
2. PostgreSQL system column `xmin` was mapped into SQLite as a required row-version column.
3. Offline answer settlement used `SELECT ... FOR UPDATE` for every relational provider, although SQLite does not support it.

Repairs:

- SQLite uses `CURRENT_TIMESTAMP`; PostgreSQL keeps the existing UTC default.
- `xmin` remains a concurrency token only for Npgsql.
- `FOR UPDATE` remains enabled for Npgsql; SQLite uses the existing serializable transaction and normal LINQ query path.

### AdminApiClient URI classification

`Uri.TryCreate("/health", UriKind.Absolute, ...)` can interpret the value as an absolute `file:` URI. The client now treats only absolute HTTP/HTTPS URIs as independent of `BaseAddress`.

### StepEngine equation parsing

The simple `x + B = C` regex previously ran before `Ax ± B = C`, so `2x + 4 = 18` was partially parsed as `x + 4 = 18`. Complex coefficient equations are now matched first, producing `x = 7`.

### Stale and brittle tests

- Two endpoint tests now assert `SafeClientErrorResponse.GenericInternalError` instead of stale duplicated text.
- The idempotency observability test normalizes a trailing slash before locating route metadata, while retaining the authorization-policy assertion.
- The former InMemory test that expected PostgreSQL `xmin` was split into explicit Npgsql and non-Npgsql model contracts.

## Files changed

### Runtime/provider

- `src/MathLearning.Admin/Services/AdminApiClient.cs`
- `src/MathLearning.Application/Helpers/StepEngine.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`

### Tests

- `tests/MathLearning.Tests/Endpoints/AnalyticsEndpointContractTests.cs`
- `tests/MathLearning.Tests/Endpoints/ExplanationEndpointContractTests.cs`
- `tests/MathLearning.Tests/Endpoints/IdempotencyObservabilityAuthorizationTests.cs`
- `tests/MathLearning.Tests/Services/AdminQuestionValidationTests.cs`

### Queue/evidence

- `docs/prompt_queues/backend_failing_test_followups_2026_07_11.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `.ai/runs/2026-07-11-BACKEND-FAILING-TESTS-001-evidence.md`

Temporary branch-only workflows used to apply and validate the patch were deleted before merge.

## Validation progression

- Run `29149157453`, artifact `8247652200`: **65/72 passed**; exposed SQLite `xmin` mapping.
- Run `29149276258`, artifact `8247686067`: **70/72 passed**; exposed SQLite `FOR UPDATE` usage.
- Run `29149413427`, artifact `8247733504`: focused **72/72 passed**; full suite **994/995 passed**; exposed stale provider contract test.
- Run `29149563136`, artifact `8247778512`:
  - Release build passed;
  - focused original failure groups: **72 passed, 0 failed**;
  - complete project: **995 passed, 0 failed**;
  - validated runtime/test patch committed as `f4f8fd175f5beb9d7e1eb5b6f74376401f5831c6`.
- Run `29149806077`, artifact `8247841295`:
  - Release build passed;
  - complete project after splitting the provider test: **996 passed, 0 failed, 0 skipped**;
  - repository-wide evidence debt captured.
- Final targeted validation run `29149913936`:
  - current run-log strict validation passed;
  - repository-wide evidence debt was captured without weakening the validator;
  - restore passed;
  - Release build passed;
  - complete backend test project passed;
  - validation artifact uploaded successfully.

### Commands

```text
dotnet restore MathLearning.slnx
dotnet build MathLearning.slnx -c Release --no-restore
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --no-build --logger "trx;LogFileName=mathlearning-tests.trx" --results-directory artifacts/test-results
```

Focused original failure groups were also run with a fully qualified name filter and passed **72/72** before the full project was executed.

## Evidence validation

The current run log passed strict targeted validation in run `29149913936`.

The repository-wide referenced evidence command was also executed and retained:

```text
python scripts/validate_agent_evidence.py --referenced-run-logs-only
```

It reports **140 historical failures and 4 warnings** in older Done rows and older run logs. No finding belongs to this run log. The existing canonical owner is `BACKEND-LATEST-EVIDENCE-002`, so no duplicate evidence-repair prompt was added.

## Queue updated

Added and indexed:

- `BACKEND-MIGRATION-001` in `docs/prompt_queues/backend_failing_test_followups_2026_07_11.md`.

The prompt requires:

- clean PostgreSQL migration validation;
- already-upgraded database validation;
- exact FK/PK/index and delete-action checks;
- idempotent migration SQL generation and review;
- full build/test execution;
- startup readiness smoke;
- a green exact-SHA `Database Validation` workflow.

It is linked to `BACKEND-TEST-032` rather than duplicating the broader PostgreSQL provider lane.

## What was done

- Classified all 35 original failures from executable evidence.
- Fixed two genuine runtime defects.
- Made provider-specific model and SQL behavior explicit.
- Preserved PostgreSQL locking and concurrency semantics.
- Corrected stale or brittle test expectations without weakening contracts.
- Ran the affected group and the complete backend test project.
- Added a complete queue prompt for the independent migration-chain blocker.
- Squash-merged PR #5 to `main` as `6cdff4c7fbeb595ed29fc11b4641d7b9fe488100`.
- Finalized this evidence on `main` with a docs-only commit after the validated merge.

## What was missed / intentionally deferred

The standard `Database Validation` workflow remains red before its normal test step because of a historical cosmetics migration-chain defect:

- `20260309091241_AddCosmeticSystem` creates foreign keys through raw PostgreSQL SQL with provider-generated names.
- `20260624133144_AlignCosmeticsMobileDataModel` attempts to drop hard-coded EF-style names.
- A clean database fails with PostgreSQL `42704` at `FK_user_avatar_configs_UserProfiles_UserId`.

This was intentionally separated because changing historical migrations requires proof for both clean and already-upgraded databases. It is owned by `BACKEND-MIGRATION-001`.

Repository-wide evidence lint also remains red for historical records and is owned by `BACKEND-LATEST-EVIDENCE-002`.

## Mistakes observed

- BACKEND-MISTAKE-VALIDATION-001 — the relational tests had not been executed against the current provider-aware model before the previous coverage claim.
- BACKEND-MISTAKE-VALIDATION-002 — `Database.IsRelational()` was treated as proof that PostgreSQL-only SQL was valid for SQLite.
- BACKEND-MISTAKE-EVIDENCE-001 — retained TRX/log artifacts were required to expose each cascading provider layer.
- BACKEND-MISTAKE-QUEUE-001 — active queues were searched before reserving the migration prompt and before declining a duplicate evidence prompt.

## Waste categories

- connector-only repository access;
- standard workflow blocks tests behind schema-from-zero;
- cascading provider symptoms required iterative runs;
- temporary workflows were required for full, untruncated logs.

## Where time/context was wasted

- Initial logs were truncated before actionable stack traces.
- Fixing the timestamp default exposed `xmin`; fixing `xmin` exposed `FOR UPDATE`.
- A stale InMemory/Npgsql assertion required one additional complete run.
- Repository-wide evidence lint includes historical debt unrelated to this patch.

## Why waste happened

- Provider behavior was guarded with broad relational checks instead of explicit capabilities.
- Standard CI serializes schema validation before tests.
- The evidence validator intentionally scans referenced historical records.

## What the next agent should avoid

- Do not use `Database.IsRelational()` as permission for PostgreSQL-only SQL.
- Do not remove Npgsql `xmin` or `FOR UPDATE` semantics merely to satisfy SQLite.
- Do not weaken or skip the schema gate.
- Do not add a later migration that a clean database cannot reach.
- Do not duplicate `BACKEND-TEST-032`, `BACKEND-MIGRATION-001`, or `BACKEND-LATEST-EVIDENCE-002`.

## Follow-up prompt

- `BACKEND-MIGRATION-001` — repair cosmetics FK-name drift with clean and upgraded PostgreSQL proof.
- Existing `BACKEND-LATEST-EVIDENCE-002` — reconcile historical evidence-lint debt.

## Completion %

- **96%**

The requested failing-test repair is complete and the complete backend test project is green. Completion is capped because the independent migration/schema workflow and historical evidence lint remain red under explicit queue owners.

## Residual risk

- `BACKEND-TEST-032` remains necessary for a broader authoritative PostgreSQL concurrency/locking lane.
- Historical migration edits carry deployed-database risk and must follow `BACKEND-MIGRATION-001` dual-path validation.
- Historical evidence debt remains governed by `BACKEND-LATEST-EVIDENCE-002`.

## Commit SHAs

- validated runtime/test repair: `f4f8fd175f5beb9d7e1eb5b6f74376401f5831c6`;
- queue prompt creation: `615706a5e602da75a513d63549ff4eaf557dd489`;
- central queue update: `d9d3348467381b9bcd4e460c536fdedafd8eac87`;
- final temporary workflow removal: `17b1c24f3afb1350291ff1e06334c0246994a4b1`;
- squash merge to `main`: `6cdff4c7fbeb595ed29fc11b4641d7b9fe488100`;
- evidence finalization: the commit containing this update.

## Cross-repo sync

Cross-repo impact: none. No mobile-facing request/response payload, error code, retry/conflict behavior or persistence contract changed.
Other repos checked: not required for this provider/test-only boundary.
Other repo docs touched: none.
Deferred sync reason: not applicable.
Follow-up prompt: backend-only `BACKEND-MIGRATION-001`.

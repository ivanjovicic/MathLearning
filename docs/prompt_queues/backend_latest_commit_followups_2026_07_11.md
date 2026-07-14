# Backend Latest Commit Follow-up Queue — 2026-07-11

Target repo: `ivanjovicic/MathLearning`  
Reviewed head before this queue: `2ee5ad8260334ca1984bc1c29c2f5bdf7bba486c`  
Run evidence: `.ai/runs/2026-07-11-BACKEND-LATEST-REVIEW-002-evidence.md`

## Review result

The latest backend work is useful but not yet closed:

- the July 3 coverage pass added **38 executable test cases** and three runtime hardening changes;
- the same evidence explicitly states that `dotnet build` and `dotnet test` were not run;
- the active test table currently contains approximately:
  - **6 validated packages** or previously validated packages;
  - **19 implemented/runtime-fixed packages still needing validation**;
  - **17 prompt-ready or confirmed-open packages**;
- all **9** current performance/bug follow-ups, BE-PERF-009 through BE-PERF-017, remain `Prompt-ready`;
- several queues describe the same implementation boundary under different IDs;
- the latest run logs do not link a successful `Database Validation` workflow run and artifact set to the reviewed head.

This queue therefore prioritizes closure and canonical ownership before another broad audit.

## What was actually completed in the latest implementation pass

| Package | Implemented result | Remaining gap |
|---|---|---|
| BACKEND-TEST-024 | Injectable/shared maintenance service, read-only GET statistics, cancellation and positive admin tests. | No executable validation; only process-local non-overlap; distributed lock/audit/safe error projection remain open. |
| BACKEND-TEST-028 | Shared checked pagination helper, analytics/bug caps and extreme-value tests. | No executable validation; analytics still fetches a bounded prefix and slices in memory. |
| BACKEND-TEST-029 | Analytics/recommendation auth, user-scope, shape, paging and safe-error tests. | No executable validation; no PostgreSQL query budget or DB-level pagination. |
| BACKEND-TEST-030 | Stable safe explanation not-found responses and endpoint tests. | No executable validation; expensive input, cost and rate guards remain open. |
| BACKEND-TEST-035 | Direct test-auth default/anonymous/role tests. | No executable validation; repository-wide privileged-route metadata audit remains open. |
| BACKEND-TEST-036 | Broad high-value pure-logic/startup/formatting package. | Status remains runtime-fixed/tests added but unvalidated. |

## Highest-risk work still not implemented

1. BACKEND-TEST-012 — refresh-token generator/model/snapshot drift.
2. BACKEND-TEST-032 — authoritative PostgreSQL provider test lane.
3. BACKEND-TEST-023 + BE-PERF-016 — one canonical outbox claim/lease/backoff/dead-letter implementation.
4. BACKEND-TEST-022 — durable analytics ingest handoff after authoritative settlement.
5. BE-PERF-012 + BACKEND-TEST-033 — adaptive mutation exactly-once settlement and cancellation/rollback proof.
6. BE-PERF-015 + BACKEND-TEST-032/033 — practice answer/completion concurrency.
7. BACKEND-TEST-031 + BE-PERF-009 — one canonical weakness scheduler implementation.

## New prompts

| ID | Priority | Status | Purpose |
|---|---:|---|---|
| BACKEND-LATEST-VALIDATION-002 | P0 | Validated | Build and execute the latest July 3 implementation/test batch; `dotnet build MathLearning.slnx -c Release --no-restore` passed with 0 errors/5 warnings and `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "MaintenanceEndpoint|AnalyticsEndpoint|ExplanationEndpoint|TestAuthHandlerTests|PaginationBounds|ExtremePagination|BugReportServicePagination|UserIdGuidMapperTests|IdempotencyObservability|DatabaseSchemaVersionGuard|WeaknessScoring|InlineLatex|StepEngine|MathContentSanitizer|TranslationHelper|QuestionEntityTests"` passed 272/272. Run log: `.ai/runs/2026-07-13-BACKEND-LATEST-VALIDATION-002-evidence.md`. |
| BACKEND-LATEST-WORKFLOW-002 | P0/P1 | Validation failed | `Database Validation` run `29150275641` failed on schema-from-zero migration `20260624133144_AlignCosmeticsMobileDataModel` with missing constraint `FK_user_avatar_configs_UserProfiles_UserId`; tests/coverage/startup smoke were skipped and no artifacts were produced. Run log: `.ai/runs/2026-07-13-BACKEND-LATEST-WORKFLOW-002-evidence.md`. |
| BACKEND-LATEST-EVIDENCE-002 | P1 | Done 75% | Linted the latest referenced July 3 evidence logs, added missing `Commit SHA:` fields, and reconciled completion caps; the referenced-only validator still reports older legacy queue/log debt outside the July 3 set. Run log: `.ai/runs/2026-07-13-BACKEND-LATEST-EVIDENCE-002-evidence.md`. |
| BACKEND-LATEST-QUEUE-002 | P1 | Done | Canonical ownership/dependency mapping added across overlapping BACKEND-TEST and BE-PERF prompts, including duplicate-risk search rules and linked-owner evidence guidance. Run log: `.ai/runs/2026-07-14-BACKEND-LATEST-QUEUE-002-evidence.md`. |

---

## BACKEND-LATEST-VALIDATION-002 — Execute and repair the latest implementation batch

Priority: P0  
Run mode: validation-first, minimal repair  
Risk: recently committed tests/runtime fixes may not compile or may assert the wrong contract.

### Goal

Turn the latest `Implemented / Needs validation` claims into checked evidence before starting another runtime package.

### Read first

- `AGENTS.md`
- `docs/AGENT_SHARED_OPERATING_STANDARD.md`
- `docs/AGENT_RUN_LOG_ENFORCEMENT.md`
- `docs/BACKEND_REGRESSION_GUARDRAILS.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-AUDIT-003-evidence.md`
- per-package evidence for BACKEND-TEST-024, 028, 029, 030, 035 and 036
- `docs/prompt_queues/backend_test_coverage.md`

### Owned validation scope

- `tests/MathLearning.Tests/Endpoints/MaintenanceEndpointContractTests.cs`
- `tests/MathLearning.Tests/Endpoints/AnalyticsEndpointContractTests.cs`
- `tests/MathLearning.Tests/Endpoints/ExplanationEndpointContractTests.cs`
- `tests/MathLearning.Tests/Helpers/TestAuthHandlerTests.cs`
- `tests/MathLearning.Tests/Helpers/PaginationBoundsTests.cs`
- `tests/MathLearning.Tests/Endpoints/ExtremePaginationEndpointTests.cs`
- `tests/MathLearning.Tests/Services/BugReportServicePaginationTests.cs`
- BACKEND-TEST-036 test files listed in the central queue
- runtime files touched by those packages, only when a failing test/build proves repair is required

### Required work

1. Create `.ai/runs/<date>-BACKEND-LATEST-VALIDATION-002-evidence.md` before edits.
2. Record the starting commit SHA and working tree state.
3. Run:

```text
dotnet restore MathLearning.slnx
dotnet build MathLearning.slnx -c Release --no-restore
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "MaintenanceEndpoint|AnalyticsEndpoint|ExplanationEndpoint|TestAuthHandlerTests|PaginationBounds|ExtremePagination|BugReportServicePagination|UserIdGuidMapperTests|IdempotencyObservability|DatabaseSchemaVersionGuard|WeaknessScoring|InlineLatex|StepEngine|MathContentSanitizer|TranslationHelper|QuestionEntityTests"
```

4. If compilation or tests fail, make the smallest repair that preserves documented contracts.
5. Do not expand into BACKEND-TEST-042…047 or BE-PERF implementation unless a direct regression in the latest batch requires it.
6. Run the focused command again after every repair.
7. Run the full test suite only after the focused set is green:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --no-build --logger "trx;LogFileName=mathlearning-tests.trx" --results-directory artifacts/test-results --collect:"XPlat Code Coverage" --settings tests/MathLearning.Tests/coverage.runsettings
```

8. Update each affected central queue row separately:
   - `Validated` only with exact successful command and result;
   - `Validation failed` with failure class and follow-up;
   - keep `Needs validation` if the environment blocks execution.
9. Update per-package evidence with exact test count, command, provider and commit SHA. Do not replace prior history.

### Required checks

- no false anonymous coverage caused by default test authentication;
- no contract change from pagination normalization;
- GET maintenance tests prove no rebuild invocation;
- explanation public errors contain no raw exception text;
- focused tests are deterministic and do not rely on sleeps/external services;
- build has no new warnings treated as errors.

### Completion rule

This prompt is not complete from code inspection. It requires a successful build plus the focused test set, or an explicit validation-failed state with exact errors and a narrowly scoped repair prompt.

---

## BACKEND-LATEST-WORKFLOW-002 — Bind `main` to Database Validation evidence

Priority: P0/P1  
Run mode: GitHub Actions validation and artifact review

### Goal

Prove whether `.github/workflows/database-validation.yml` actually validates the reviewed backend head and expose any workflow-only failure hidden by connector-only static review.

### Inspect

- `.github/workflows/database-validation.yml`
- latest `main` commit after BACKEND-LATEST-VALIDATION-002 changes, if any
- GitHub Actions run, jobs, logs and artifacts for `Database Validation`
- `artifacts/test-results`
- `artifacts/coverage-report`
- generated idempotent migration artifact

### Required work

1. Create `.ai/runs/<date>-BACKEND-LATEST-WORKFLOW-002-evidence.md`.
2. Locate the `Database Validation` run for the exact target SHA. If no run exists, trigger one through the supported repository workflow mechanism without changing runtime code.
3. Record:
   - workflow run URL/id;
   - exact commit SHA;
   - build result;
   - schema-from-zero result;
   - test result and count;
   - coverage artifact presence and summary;
   - idempotent migration artifact presence;
   - startup readiness smoke result.
4. If the workflow fails, classify before editing:
   - restore/build;
   - migration/schema;
   - test compile;
   - test assertion;
   - coverage/report generation;
   - startup smoke;
   - infrastructure/transient.
5. Repair only the proven workflow or code defect. Do not weaken assertions, skip provider-sensitive tests or convert failures to warnings merely to obtain green status.
6. Re-run the failed job/workflow and record the final result.
7. Link the successful run/artifacts from the central queue and relevant run logs.

### Important provider check

The workflow starts PostgreSQL 16, but the ordinary test step is not automatically proof that every concurrency test uses PostgreSQL. Record which suites use InMemory, SQLite and PostgreSQL. BACKEND-TEST-032 remains open until provider-sensitive tests use an explicit PostgreSQL fixture/lane.

### Completion rule

A green badge without the exact SHA and artifact review is insufficient. A local test run also does not replace this workflow evidence.

---

## BACKEND-LATEST-EVIDENCE-002 — Latest run-log lint and status reconciliation

Priority: P1  
Run mode: docs/evidence repair only

### Goal

Ensure the latest agent logs accurately distinguish static review, committed implementation, executable validation and runtime proof.

### Inspect

- `scripts/validate_agent_evidence.py`
- `.ai/runs/2026-07-03-BACKEND-TEST-AUDIT-003-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-PERF-AUDIT-004-evidence.md`
- per-package July 3 evidence logs for 012, 024, 028, 029, 030, 035 and 036
- queue rows that reference those logs
- `docs/AGENT_RUN_LOG_ENFORCEMENT.md`

### Required work

1. Create `.ai/runs/<date>-BACKEND-LATEST-EVIDENCE-002-evidence.md`.
2. Run:

```text
python scripts/validate_agent_evidence.py --referenced-run-logs-only
```

3. Repair missing mandatory fields without inventing values.
4. Verify every referenced log has:
   - prompt ID and queue;
   - model/client;
   - exact files changed;
   - validation run or explicit reason not run;
   - mistakes/waste/missed/follow-up/residual risk;
   - commit SHA(s);
   - cross-repo status.
5. Check that completion percentages comply with the score cap for the actual evidence level.
6. Check that queue statuses match evidence:
   - committed tests without execution remain `Implemented / Needs validation`;
   - docs-only audits do not imply runtime fixes;
   - static provider findings remain confirmed drift/risk, not validated bugs in production.
7. Run the validator again and record exact output.
8. Do not modify runtime or tests in this prompt.

### Completion rule

Referenced mode must pass, or every remaining failure must be listed with the exact file and why it cannot be repaired safely.

---

## BACKEND-LATEST-QUEUE-002 — Canonical prompt ownership and dependency map

Priority: P1  
Run mode: queue architecture/docs only

### Goal

Prevent two agents from independently implementing the same risk under different prompt IDs and producing conflicting migrations, tests or evidence.

### Required canonical mappings

At minimum reconcile these overlaps:

| Canonical runtime owner | Linked/supporting prompt | Rule |
|---|---|---|
| BACKEND-TEST-031 | BE-PERF-009 | One weakness scheduler implementation and one primary evidence log. Performance measurements can be appended, not reimplemented. |
| BACKEND-TEST-023 | BE-PERF-016 | One outbox schema/claim/backoff/dead-letter implementation. PostgreSQL proof is shared. |
| BACKEND-TEST-042 | maintenance portion of operational/performance work | Distributed lock/audit/safe errors must be one implementation across API, CLI and worker. |
| BACKEND-TEST-043 | BE-PERF-014 force-refresh/cost boundary | Input/rate/cost guard and cache policy must not diverge. |
| BACKEND-TEST-032 | BE-PERF-012, BE-PERF-015, BE-PERF-016 provider proof | PostgreSQL fixture/lane is a prerequisite, not a duplicate implementation package. |
| BACKEND-TEST-033 | BE-PERF-012 and BE-PERF-015 cancellation/rollback proof | Reuse deterministic barriers/interceptors rather than separate failure-injection frameworks. |
| BACKEND-TEST-045 | analytics part of recent pagination fixes | It supersedes endpoint-only prefix slicing for scalability, while preserving BACKEND-TEST-028 contract bounds. |

### Required work

1. Create `.ai/runs/<date>-BACKEND-LATEST-QUEUE-002-evidence.md`.
2. Inventory all active rows in:
   - `backend_test_coverage.md`;
   - `backend_test_followups_2026_07_03.md`;
   - `backend_test_followups_pass2_2026_07_03.md`;
   - `backend_performance_optimization.md`;
   - `backend_performance_followups_2026_07_03.md`;
   - critical and second-pass risk queues.
3. For each overlap, assign:
   - one canonical runtime owner;
   - prerequisite IDs;
   - supporting test/performance IDs;
   - one primary evidence log;
   - status propagation rule.
4. Add `Linked to`, `Depends on`, `Satisfies` or `Superseded by` fields to affected rows without deleting historical prompt text.
5. Reserve IDs atomically before publishing new prompts.
6. Add a rule: an agent must search all active queues for the entity/service/risk before allocating a new ID.
7. Add a machine-checkable duplicate-risk inventory if feasible, but do not build a complex new tool unless simple validation is insufficient.

### Ownership rule added by this prompt

- Before claiming a new backend prompt ID, search active queues for the prompt ID, endpoint, service, entity and risk phrase.
- Search existing `Canonical owner`, `Linked to`, `Depends on`, `Satisfies` and `Superseded by` markers before allocating a new implementation lane.
- If the same runtime risk already exists, extend the canonical owner and add only a linked/supporting row instead of spawning a second implementation package.

### Completion rule

The map must make it impossible to interpret BACKEND-TEST-023 and BE-PERF-016, or BACKEND-TEST-031 and BE-PERF-009, as two independent runtime implementations.

## Canonical next execution order

After the four closure prompts above:

1. **BACKEND-TEST-032** — establish the PostgreSQL provider fixture/lane.
2. **BACKEND-TEST-023 as canonical owner, satisfying BE-PERF-016** — implement outbox claim/lease/backoff/dead-letter.
3. **BACKEND-TEST-022** — durable quiz/offline analytics ingest handoff, reusing the outbox contract.
4. **BE-PERF-012 with BACKEND-TEST-033 and mobile contract sync** — adaptive exactly-once settlement.
5. **BE-PERF-015 with BACKEND-TEST-032/033** — practice answer/completion concurrency.
6. **BACKEND-TEST-031 as canonical owner, satisfying BE-PERF-009** — bounded/durable weakness scheduling.
7. Continue maintenance, explanation, pagination and privileged-route prompts by risk.

## Stop rules

- Do not start another broad backend audit until the latest implementation batch has executable validation.
- Do not mark a row `Validated` from static inspection or committed tests alone.
- Do not create a second implementation ID for an already-owned risk.
- Do not claim PostgreSQL concurrency proof from SQLite/InMemory tests or from merely starting a PostgreSQL service in CI.
- Do not change mobile-facing retry/conflict semantics without cross-repo verification and recorded sync.

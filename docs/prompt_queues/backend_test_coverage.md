# Backend Test Coverage Queue

Last aligned: 2026-07-11  
Target repo: `ivanjovicic/MathLearning`

## Purpose

Increase backend confidence by risk, not by chasing superficial coverage percentage.

Coverage audits:

- [`../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`](../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md)
- [`../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md`](../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md)

Prompt queues:

- [`backend_test_followups_2026_07_03.md`](backend_test_followups_2026_07_03.md) — BACKEND-TEST-022…035
- [`backend_test_followups_pass2_2026_07_03.md`](backend_test_followups_pass2_2026_07_03.md) — status overrides and BACKEND-TEST-042…047
- [`backend_latest_commit_followups_2026_07_11.md`](backend_latest_commit_followups_2026_07_11.md) — latest implementation validation, workflow evidence, run-log lint and canonical queue ownership

## Read first

- `../../AGENTS.md`
- `../BACKEND_TEST_COVERAGE_STRATEGY.md`
- both current coverage audits
- `../BACKEND_REGRESSION_GUARDRAILS.md`
- `../AGENT_RUN_LOG_ENFORCEMENT.md`
- `../ai/learning/MISTAKE_LEDGER.md`

## Rules

- One critical flow or tightly related pure-logic package per prompt.
- Add the smallest tests that prove the invariant.
- Prefer endpoint/integration tests for auth and contract behavior.
- Use SQLite/PostgreSQL for transaction, FK, uniqueness and concurrency guarantees.
- Record exact validation or why it did not run.
- Do not mark Done without `.ai/runs` evidence.
- A committed test remains **Implemented / Needs validation** until `dotnet test` or checked CI evidence exists.
- Failure-injection tests should fail after SQL but before commit when rollback is the invariant.
- Explicit anonymous tests must use `X-Test-Anonymous: true`.
- Preserve existing endpoint normalization semantics while adding shared bounds.
- Do not set coverage thresholds before a successful measured baseline.
- Search all active test/performance/risk queues before allocating a new ID; overlapping risks require one canonical runtime owner.

## Active prompts

| ID | Status | Purpose |
|---|---|---|
| BACKEND-TEST-CORE-001 | Needs validation | Daily Run/cosmetics trust boundary, economy state machine, refresh-token primitives, relational constraints and initial coverage collection. |
| BACKEND-TEST-002 | Covered / Needs validation | Season settlement response snapshot and exact replay-body truth. |
| BACKEND-TEST-003 | Implemented / Needs validation | Operation identity helper/HTTP characterization and empty offline-session replay behavior. |
| BACKEND-TEST-004 | Validated | Offline timestamp normalization and replay bounds: 25 passed. |
| BACKEND-TEST-005 | Validated | Safe auth/global errors: 41 passed; focused subset 6 passed. |
| BACKEND-TEST-006 | Validated / anonymous branch strengthened | Monitoring/log authorization, redaction and bounds: 9 passed previously; explicit anonymous branch needs rerun. |
| BACKEND-TEST-007 | Validated | Public identity allowlists: 10 passed. |
| BACKEND-TEST-008 | Validated | Avatar upload/static-serving safety: 43 passed. |
| BACKEND-TEST-009 | Implemented / Needs validation | Relational idempotency constraints, rollback and duplicate-insert recovery. |
| BACKEND-TEST-010 | Validated | Bounded reads and enum normalization: 70 passed. |
| BACKEND-TEST-011 | Implemented / Workflow validation needed | GitHub summary plus HTML/merged Cobertura coverage artifact. |
| BACKEND-TEST-012 | Confirmed drift / Needs safe patch | Refresh-token generator 88 chars vs EF model/snapshot 64 and migration target 128. |
| BACKEND-TEST-013 | Ready / P0 contract decision | Require, gate or isolate missing operation identity. |
| BACKEND-TEST-014 | Implemented / Needs validation | Shared/cosmetics idempotency state machines and canonical payload semantics. |
| BACKEND-TEST-015 | Implemented / Needs validation | Real HTTP refresh-token rotation race against relational SQLite. |
| BACKEND-TEST-016 | Implemented / Needs validation | Transaction helper commit, rollback-after-SQL, retry, exhaustion and cancellation. |
| BACKEND-TEST-017 | Implemented / Needs validation | Mobile registration relational rollback and clean retry. |
| BACKEND-TEST-018 | Implemented / Needs validation | Offline-submit relational rollback, retry and replay. |
| BACKEND-TEST-019 | Implemented / Needs validation | QuizAttempt ingest aggregation, rollback, cancellation and scheduler ordering. |
| BACKEND-TEST-020 | Runtime-fixed / Needs validation | Bug report user/admin authorization boundaries. |
| BACKEND-TEST-021 | Runtime-fixed / Needs validation | Maintenance routes require exact admin policy. |
| BACKEND-TEST-022 | P0 / Prompt-ready | Durable/idempotent analytics ingest delivery after authoritative settlement. |
| BACKEND-TEST-023 | P0/P1 / Prompt-ready | Canonical runtime owner for multi-instance outbox claim, duplicate-publish and poison-message safety; satisfies linked BE-PERF-016 when fully implemented and measured. |
| BACKEND-TEST-024 | Runtime-fixed / Needs validation | Injectable shared maintenance service, read-only GET stats, cancellation, local non-overlap and positive admin tests. |
| BACKEND-TEST-025 | P1 / Prompt-ready | Bug-report input/screenshot validation and orphan-storage compensation. |
| BACKEND-TEST-026 | P1 / Prompt-ready | Minimize public health/metrics/schema/job information. |
| BACKEND-TEST-027 | P1/P2 / Prompt-ready | Decide whether to wire, merge or remove dead `QuestionEndpoints`. |
| BACKEND-TEST-028 | Runtime-fixed / Needs validation | Shared checked pagination bounds, analytics/bug migration and extreme-value tests. |
| BACKEND-TEST-029 | Implemented / Needs validation | Analytics/recommendation auth, user scope, paging, shape and safe-error endpoint tests. |
| BACKEND-TEST-030 | Runtime-fixed / Needs validation | Explanation validation contracts and stable safe not-found messages. |
| BACKEND-TEST-031 | P1 / Prompt-ready | Canonical runtime owner for weakness scheduler durability, deduplication and backpressure; satisfies linked BE-PERF-009 when fully implemented and measured. |
| BACKEND-TEST-032 | P0/P1 / Prompt-ready | PostgreSQL provider-specific concurrency/locking/constraint lane; prerequisite proof for BE-PERF-012, 015 and 016. |
| BACKEND-TEST-033 | P1 / Prompt-ready | Cancellation/rollback matrix for canonical P0 mutations; supporting proof for BE-PERF-012 and 015. |
| BACKEND-TEST-034 | P1/P2 / Prompt-ready | Legacy route parity/deprecation and duplicate-settlement prevention. |
| BACKEND-TEST-035 | Implemented / Needs validation | Direct test-auth default/anonymous/role contract tests. |
| BACKEND-TEST-036 | Runtime-fixed + tests / Needs validation | Identity mapping, observability, startup/schema decisions, weakness math, LaTeX preservation, sanitization, step generation, translation fallback and question invariants. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-036-evidence.md`. |
| BACKEND-TEST-042…047 | Prompt-ready | Distributed maintenance, explanation cost/input limits, deterministic scheduler, DB/cursor analytics paging, remaining pagination inventory and privileged-route metadata audit. |
| BACKEND-LATEST-VALIDATION-002 | P0 / Prompt-ready | Execute and minimally repair the latest July 3 implementation/test batch before more runtime work. |
| BACKEND-LATEST-WORKFLOW-002 | P0/P1 / Prompt-ready | Link exact `main` SHA to Database Validation jobs, logs and artifacts. |
| BACKEND-LATEST-EVIDENCE-002 | P1 / Prompt-ready | Lint latest referenced run logs and reconcile evidence/status/score claims. |
| BACKEND-LATEST-QUEUE-002 | P1 / Prompt-ready | Canonical ownership and dependency map across overlapping test/performance queues. |

IDs 037–041 are reserved/occupied by parallel coverage work and are not reused by the pass-2 follow-up queue.

## Existing validated coverage — do not duplicate blindly

| Area | Evidence |
|---|---|
| Offline timestamps | `.ai/runs/2026-07-01-BACKEND-CRIT-007-evidence.md` — 25 passed. |
| Safe errors | `.ai/runs/2026-06-24-BACKEND-CRIT-001-evidence.md` — 41 passed; focused subset 6 passed. |
| Monitoring/log security | `.ai/runs/2026-06-24-BACKEND-CRIT-002-evidence.md` — 9 passed before explicit-anonymous update. |
| Public identity | `.ai/runs/2026-07-01-BACKEND-CRIT-003-evidence.md` — 10 passed. |
| Avatar safety | `.ai/runs/2026-06-24-BACKEND-CRIT-004-evidence.md` — 43 passed. |
| Bounded reads | `.ai/runs/2026-07-01-BACKEND-CRIT-008-evidence.md` — 70 passed. |
| Proxy/authoring/adaptive/jobs/admin seed/version race | `.ai/runs/2026-06-24-BACKEND2-*` evidence — 82 targeted tests. |

## Focused validation commands

### Settlement, identity and relational mutation packages

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~Season|FullyQualifiedName~OperationIdentity|FullyQualifiedName~RelationalIdempotency|FullyQualifiedName~AuthRefreshRelationalConcurrencyTests|FullyQualifiedName~AuthMobileRegistrationRelationalAtomicityTests|FullyQualifiedName~OfflineBatchRelationalAtomicityTests|FullyQualifiedName~ApiDbTransactionHelpersRelationalTests"
```

### Direct idempotency state machines

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~IdempotencyLedgerServiceTests|FullyQualifiedName~CosmeticsIdempotencyServiceTests|FullyQualifiedName~IdempotencyPayloadCanonicalizerTests"
```

### First and second coverage-audit packages

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "QuizAttemptIngestServiceRelationalTests|BugEndpointAuthorizationTests|MaintenanceEndpointAuthorizationTests|MonitoringLogAuthorizationTests|MaintenanceEndpointContractTests|AnalyticsEndpointContractTests|ExplanationEndpointContractTests|TestAuthHandlerTests|PaginationBoundsTests|ExtremePaginationEndpointTests|BugReportServicePaginationTests"
dotnet build MathLearning.slnx -c Release
```

### BACKEND-TEST-036 parallel high-value package

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~UserIdGuidMapperTests|FullyQualifiedName~IdempotencyObservabilityServiceTests|FullyQualifiedName~IdempotencyObservabilityAuthorizationTests|FullyQualifiedName~DatabaseSchemaVersionGuardTests|FullyQualifiedName~WeaknessScoringTests|FullyQualifiedName~InlineLatexFormatterTests|FullyQualifiedName~InlineLatexEndpointContractTests|FullyQualifiedName~StepEngineTests|FullyQualifiedName~MathContentSanitizerTests|FullyQualifiedName~TranslationHelperTests|FullyQualifiedName~QuestionEntityTests"
dotnet build MathLearning.slnx -c Release
```

## Coverage workflow

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --logger "trx;LogFileName=mathlearning-tests.trx" --results-directory artifacts/test-results --collect:"XPlat Code Coverage" --settings tests/MathLearning.Tests/coverage.runsettings
```

The first successful ReportGenerator artifact must be reviewed before setting line/branch thresholds.

## Next execution order

1. Run BACKEND-LATEST-VALIDATION-002 and repair only proven compile/test failures.
2. Run BACKEND-LATEST-WORKFLOW-002 against the exact resulting `main` SHA and review artifacts.
3. Run BACKEND-LATEST-EVIDENCE-002 and BACKEND-LATEST-QUEUE-002 to close evidence and duplicate-ownership drift.
4. Apply BACKEND-TEST-012 refresh-token model/snapshot correction.
5. Run BACKEND-TEST-032 PostgreSQL provider-specific lane.
6. Run BACKEND-TEST-023 as canonical outbox owner, satisfying linked BE-PERF-016.
7. Run BACKEND-TEST-022 durable ingest delivery.
8. Resolve BACKEND-TEST-013 operation-identity contract with mobile sync.
9. Run BACKEND-TEST-025/026/031/033/034 by risk, using canonical linked ownership.
10. Continue BACKEND-TEST-042…047 after the P0 packages.
11. Review the first coverage summary and introduce progressive thresholds only from measured evidence.

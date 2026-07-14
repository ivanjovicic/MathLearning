# Backend Test Coverage Queue

Last aligned: 2026-07-11  
Target repo: `ivanjovicic/MathLearning`

## Purpose

Increase backend confidence by risk, not by chasing superficial coverage percentage.

Coverage audits:

- [`../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`](../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md)
- [`../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md`](../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md)
- [`../BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11.md`](../BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11.md)
- [`../BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11_PASS2.md`](../BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11_PASS2.md)

Prompt queues:

- [`backend_test_followups_2026_07_03.md`](backend_test_followups_2026_07_03.md) — BACKEND-TEST-022…035
- [`backend_test_followups_pass2_2026_07_03.md`](backend_test_followups_pass2_2026_07_03.md) — status overrides and BACKEND-TEST-042…047
- [`backend_latest_commit_followups_2026_07_11.md`](backend_latest_commit_followups_2026_07_11.md) — latest implementation validation, workflow evidence, run-log lint and canonical queue ownership
- [`backend_api_db_residuals_2026_07_11.md`](backend_api_db_residuals_2026_07_11.md) — BACKEND-API-DB-001…008 for answer disclosure, quiz/progress/sync authority, offline bundles, token storage and remaining user-read queries
- [`backend_api_db_residuals_pass2_2026_07_11.md`](backend_api_db_residuals_pass2_2026_07_11.md) — BACKEND-API-DB-009…015 for cosmetics/economy entitlement, leaderboard identity/parity, account provisioning, photo avatars and pending-operation recovery
- [`backend_failing_test_followups_2026_07_11.md`](backend_failing_test_followups_2026_07_11.md) — BACKEND-MIGRATION-001 for the remaining clean/upgraded PostgreSQL cosmetics migration blocker

## Read first

- `../../AGENTS.md`
- `../BACKEND_TEST_COVERAGE_STRATEGY.md`
- current coverage/API/DB audits
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
- When a queue row is test-side only, append runtime/provider/evidence work to its canonical owner instead of opening a second implementation lane.
- Idempotency is not entitlement: a stable key cannot authorize a client-declared reward, quantity, price or source.

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
| BACKEND-TEST-012 | Validated | Refresh-token generator/model/snapshot drift resolved by aligning EF metadata and snapshot to the existing 128-char migration target, without creating a redundant migration. Verified with `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~RefreshTokenServiceSecurityTests"`: 8 passed, 0 failed, 0 skipped; `dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext --no-build`: no pending model changes. Run log: `.ai/runs/2026-07-14-BACKEND-TEST-012-evidence.md`. |
| BACKEND-TEST-013 | Ready / P0 contract decision | Require, gate or isolate missing operation identity. |
| BACKEND-TEST-014 | Implemented / Needs validation | Shared/cosmetics idempotency state machines and canonical payload semantics. |
| BACKEND-TEST-015 | Implemented / Needs validation | Real HTTP refresh-token rotation race against relational SQLite. |
| BACKEND-TEST-016 | Implemented / Needs validation | Transaction helper commit, rollback-after-SQL, retry, exhaustion and cancellation. |
| BACKEND-TEST-017 | Implemented / Needs validation | Mobile registration relational rollback and clean retry. |
| BACKEND-TEST-018 | Implemented / Needs validation | Offline-submit relational rollback, retry and replay. |
| BACKEND-TEST-019 | Implemented / Needs validation | QuizAttempt ingest aggregation, rollback, cancellation and scheduler ordering. |
| BACKEND-TEST-020 | Runtime-fixed / Needs validation | Bug report user/admin authorization boundaries. |
| BACKEND-TEST-021 | Runtime-fixed / Needs validation | Maintenance routes require exact admin policy. |
| BACKEND-TEST-022 | Runtime-fixed / Needs schema validation | Quiz/offline settlement now enqueues durable outbox ingest commands inside the authoritative transaction, keeps client success independent from async analytics delivery, and deduplicates ingest by stable `AttemptKey`; focused tests passed but `scripts/db/validate-schema.ps1` still needs a reachable local PostgreSQL instance. Run log: `.ai/runs/2026-07-14-BACKEND-TEST-022-evidence.md` |
| BACKEND-TEST-023 | Runtime-fixed / Workflow validation needed | Canonical runtime owner now uses `FOR UPDATE SKIP LOCKED`, bounded retry/dead-letter state, redacted persisted errors and hosted-service wiring; PostgreSQL proof still needs CI evidence or valid local credentials before linked BE-PERF-016 can be marked validated. Run log: `.ai/runs/2026-07-14-BACKEND-TEST-023-evidence.md` |
| BACKEND-TEST-024 | Runtime-fixed / Needs validation | Injectable shared maintenance service, read-only GET stats, cancellation, local non-overlap and positive admin tests. |
| BACKEND-TEST-025 | P1 / Prompt-ready | Bug-report input/screenshot validation and orphan-storage compensation. |
| BACKEND-TEST-026 | P1 / Prompt-ready | Minimize public health/metrics/schema/job information. |
| BACKEND-TEST-027 | P1/P2 / Prompt-ready | Decide whether to wire, merge or remove dead `QuestionEndpoints`. |
| BACKEND-TEST-028 | Runtime-fixed / Needs validation | Shared checked pagination bounds, analytics/bug migration and extreme-value tests. |
| BACKEND-TEST-029 | Implemented / Needs validation | Analytics/recommendation auth, user scope, paging, shape and safe-error endpoint tests. |
| BACKEND-TEST-030 | Runtime-fixed / Needs validation | Explanation validation contracts and stable safe not-found messages. |
| BACKEND-TEST-031 | P1 / Prompt-ready | Canonical runtime owner for weakness scheduler durability, deduplication and backpressure; satisfies linked BE-PERF-009 when fully implemented and measured. |
| BACKEND-TEST-032 | Implemented / Workflow validation needed | Shared PostgreSQL provider harness and initial authority tests are now wired for refresh-token persistence, refresh rotation race and fresh-migration schema guard; the CI workflow now exports `POSTGRES_PROVIDER_TESTS_REQUIRED=1` with the workflow PostgreSQL service. Exact provider execution still needs successful workflow or local maintenance credentials. Run log: `.ai/runs/2026-07-14-BACKEND-TEST-032-evidence.md`. |
| BACKEND-TEST-033 | P1 / Prompt-ready | Cancellation/rollback matrix for canonical P0 mutations; supporting proof for BE-PERF-012 and 015. |
| BACKEND-TEST-034 | P1/P2 / Prompt-ready | Legacy route parity/deprecation and duplicate-settlement prevention. |
| BACKEND-TEST-035 | Implemented / Needs validation | Direct test-auth default/anonymous/role contract tests. |
| BACKEND-TEST-036 | Validated | Identity mapping, observability, startup/schema decisions, weakness math, LaTeX preservation, sanitization, step generation, translation fallback and question invariants. Verified with `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "MaintenanceEndpoint|AnalyticsEndpoint|ExplanationEndpoint|TestAuthHandlerTests|PaginationBounds|ExtremePagination|BugReportServicePagination|UserIdGuidMapperTests|IdempotencyObservability|DatabaseSchemaVersionGuard|WeaknessScoring|InlineLatex|StepEngine|MathContentSanitizer|TranslationHelper|QuestionEntityTests"`: 272 passed, 0 failed, 0 skipped. Run log: `.ai/runs/2026-07-13-BACKEND-LATEST-VALIDATION-002-evidence.md`. |
| BACKEND-TEST-042…047 | Prompt-ready | Distributed maintenance, explanation cost/input limits, deterministic scheduler, DB/cursor analytics paging, remaining pagination inventory and privileged-route metadata audit. |
| BACKEND-LATEST-VALIDATION-002 | Validated | Latest July 3 implementation/test batch verified; `dotnet build MathLearning.slnx -c Release --no-restore` passed with 0 errors/5 warnings and the focused `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "MaintenanceEndpoint|AnalyticsEndpoint|ExplanationEndpoint|TestAuthHandlerTests|PaginationBounds|ExtremePagination|BugReportServicePagination|UserIdGuidMapperTests|IdempotencyObservability|DatabaseSchemaVersionGuard|WeaknessScoring|InlineLatex|StepEngine|MathContentSanitizer|TranslationHelper|QuestionEntityTests"` passed 272/272. Run log: `.ai/runs/2026-07-13-BACKEND-LATEST-VALIDATION-002-evidence.md`. |
| BACKEND-LATEST-WORKFLOW-002 | P0/P1 / Prompt-ready | Link exact `main` SHA to Database Validation jobs, logs and artifacts. |
| BACKEND-LATEST-EVIDENCE-002 | P1 / Done 75% | Linted the latest referenced July 3 evidence logs, added missing `Commit SHA:` fields, and reconciled completion caps; the referenced-only validator still reports older legacy queue/log debt outside the July 3 set. Run log: `.ai/runs/2026-07-13-BACKEND-LATEST-EVIDENCE-002-evidence.md`. |
| BACKEND-LATEST-QUEUE-002 | P1 / Done | Canonical ownership and dependency map across overlapping test/performance queues, including duplicate-risk search guidance. Run log: `.ai/runs/2026-07-14-BACKEND-LATEST-QUEUE-002-evidence.md`. |
| BACKEND-MIGRATION-001 | P0 / Prompt-ready | Repair historical cosmetics FK-name drift and prove clean plus upgraded PostgreSQL migration paths without weakening the schema gate. |
| BACKEND-API-DB-001 | P0 / Prompt-ready | Remove answer keys and complete solution material from online pre-answer quiz/SRS responses. |
| BACKEND-API-DB-002 | P0 / Prompt-ready | Require a valid user-owned issued quiz session/question before answer settlement. |
| BACKEND-API-DB-003 | P0/P1 / Prompt-ready | Replace client-authoritative progress completion/day with verifiable idempotent settlement. |
| BACKEND-API-DB-004 | P0/P1 / Prompt-ready | Scope sync operation identity and serialize same-device cursor mutation on PostgreSQL. |
| BACKEND-API-DB-005 | P1 / Prompt-ready | Correct offline bundle hint mapping and content revision/version truth. |
| BACKEND-API-DB-006 | P1 / Prompt-ready | Bound sync envelopes, persisted failure data and retention. |
| BACKEND-API-DB-007 | P1 / Prompt-ready; depends on/safely supersedes BACKEND-TEST-012 schema work | Store no reusable refresh-token secret and bound expired/revoked retention. |
| BACKEND-API-DB-008 | P1/P2 / Prompt-ready; linked to BE-PERF-013 | SQL user aggregates, indexed search and remaining zero-write GET contracts. |
| BACKEND-API-DB-009 | P0 / Runtime-fixed / Needs schema validation | Cosmetic item/fragment claim flows now require server-issued entitlements; cosmetics purchase is idempotent and server-priced. Run log: `.ai/runs/2026-07-14-BACKEND-API-DB-009-evidence.md` |
| BACKEND-API-DB-010 | P0 / Runtime-fixed | Legacy coin/power-up mutations and paid-hint aliases now return `410 Gone`; canonical hint reads are read-only after `/api/economy/hints/use`. Run log: `.ai/runs/2026-07-14-BACKEND-API-DB-010-evidence.md` |
| BACKEND-API-DB-011 | P0 / Runtime-fixed | Student leaderboard now uses string-safe ranking and scope/period-bound v2 cursors; invalid or mismatched cursors return `400`. Run log: `.ai/runs/2026-07-14-BACKEND-API-DB-011-evidence.md` |
| BACKEND-API-DB-012 | P1 / Prompt-ready; depends on BACKEND-API-DB-011 | Make Redis and DB leaderboard scope/rank/cursor/failover behavior contract-equivalent. |
| BACKEND-API-DB-013 | P1 / Prompt-ready | Use one complete account-provisioning owner and reconcile Identity/profile/token orphan states. |
| BACKEND-API-DB-014 | P1 / Prompt-ready | Retire or rebuild legacy photo avatars with string/self routes, durable storage and compensation. |
| BACKEND-API-DB-015 | P0/P1 / Prompt-ready; extends BACKEND-TEST-014/032/033 | Recover stale pending economy/cosmetics operations without allowing dual settlement. |

IDs 037–041 are reserved/occupied by parallel coverage work and are not reused by the pass-2 follow-up queue.

## Canonical ownership map

| Test prompt | Canonical performance owner | Scope note |
|---|---|---|
| BACKEND-TEST-023 | BE-PERF-016 | Outbox claim, duplicate-publish defense and poison-message lifecycle are owned on the performance side; this test row keeps the contract and regression coverage. |
| BACKEND-TEST-031 | BE-PERF-009 | Weakness scheduling owns the bounded queue and deduplication behavior; keep the test row aligned to the scheduler contract. |
| BACKEND-TEST-032 | BE-PERF-012, BE-PERF-015, BE-PERF-016 | PostgreSQL provider proof sits behind the adaptive/practice/outbox lanes and should not be re-implemented separately. |
| BACKEND-TEST-033 | BE-PERF-012 and BE-PERF-015 | Cancellation/rollback proof belongs to the canonical P0 mutation lanes. |
| BACKEND-TEST-042 | maintenance operational workstream | Distributed maintenance lock/audit coverage is the shared ops/maintenance responsibility, not a new standalone implementation lane. |
| BACKEND-TEST-043 | BE-PERF-014 | Force-refresh and cost-bound checks mirror the explanation-cache guard work. |
| BACKEND-TEST-045 | analytics pagination fixup workstream | This is the analytics side of the pagination fix and must preserve BACKEND-TEST-028 bounds. |

## Duplicate-risk search rule

- Search active queues by endpoint, service, entity, risk phrase and existing linked-owner markers before allocating a new backend ID.
- Reuse `Canonical owner`, `Linked to`, `Depends on`, `Satisfies` and `Superseded by` markers to decide whether the work is runtime ownership, provider proof or contract validation.
- If a matching runtime risk already exists, extend that canonical row and share the evidence log unless the new prompt is docs-only.

## Existing validated coverage — do not duplicate blindly

| Area | Evidence |
|---|---|
| Full backend test project after failing-test repairs | `.ai/runs/2026-07-11-BACKEND-FAILING-TESTS-001-evidence.md` — 996 passed, 0 failed; independent migration gate remains queued. |
| Offline timestamps | `.ai/runs/2026-07-01-BACKEND-CRIT-007-evidence.md` — 25 passed. |
| Safe errors | `.ai/runs/2026-06-24-BACKEND-CRIT-001-evidence.md` — 41 passed; focused subset 6 passed. |
| Monitoring/log security | `.ai/runs/2026-06-24-BACKEND-CRIT-002-evidence.md` — 9 passed before explicit-anonymous update. |
| Public identity | `.ai/runs/2026-07-01-BACKEND-CRIT-003-evidence.md` — 10 passed. |
| Avatar file safety | `.ai/runs/2026-06-24-BACKEND-CRIT-004-evidence.md` — 43 passed; does not prove durable storage/route compatibility. |
| Bounded reads | `.ai/runs/2026-07-01-BACKEND-CRIT-008-evidence.md` — 70 passed. |
| Daily Run fragment trust boundary | `.ai/runs/2026-07-02-BACKEND-TEST-CORE-001-evidence.md` — server chest/season values covered; generic non-Daily-Run grants remain queued. |
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

1. Run BACKEND-LATEST-WORKFLOW-002 against the current exact `main` SHA and review the standard workflow/artifacts.
2. Run BACKEND-LATEST-EVIDENCE-002 and BACKEND-LATEST-QUEUE-002 to close historical evidence and duplicate-ownership drift.
3. Run BACKEND-MIGRATION-001 to restore clean and upgraded PostgreSQL migration validation without weakening the schema gate.
4. Run BACKEND-API-DB-009 to stop arbitrary cosmetic item/fragment grants.
5. Validate consumer cleanup and eventual removal plan for the now-disabled BACKEND-API-DB-010 legacy coin/hint/power-up routes.
6. Run BACKEND-API-DB-011 to restore student leaderboard behavior for string/GUID Identity users.
7. Run BACKEND-API-DB-015 to make canonical economy/cosmetics operations recoverable after pending-state failures.
8. Run BACKEND-API-DB-001 to remove answer/solution disclosure from pre-answer online contracts.
9. Run BACKEND-API-DB-002 to establish quiz-session/question authority.
10. Run BACKEND-API-DB-003 to replace client-authoritative progress completion.
11. Run BACKEND-TEST-032 PostgreSQL provider-specific lane.
12. Run BACKEND-API-DB-004 for same-device sync serialization and scoped operation identity.
13. Run BACKEND-API-DB-012 for Redis/DB leaderboard parity after the string cursor contract is stable.
14. Run BACKEND-API-DB-013 for one complete account-provisioning owner and orphan reconciliation.
15. Run BACKEND-API-DB-014 to retire or repair the photo-avatar contract/storage.
16. Re-run BACKEND-TEST-023 with working PostgreSQL credentials or CI evidence, then close linked BE-PERF-016.
17. Re-run BACKEND-TEST-022 schema validation on reachable local/CI PostgreSQL, then close the durable ingest lane.
18. Resolve BACKEND-TEST-013 operation-identity contract with mobile sync.
19. Run BACKEND-API-DB-005 and 006 for offline bundle truth and sync envelope/data lifecycle.
20. Run BACKEND-API-DB-007 for refresh-token at-rest and retention protection.
21. Run BACKEND-API-DB-008 under the canonical BE-PERF-013 pure-read owner.
22. Run BACKEND-TEST-025/026/031/033/034 by risk, using canonical linked ownership.
23. Continue BACKEND-TEST-042…047 after the P0 packages.
24. Review the measured coverage summary and introduce progressive thresholds only from retained evidence.

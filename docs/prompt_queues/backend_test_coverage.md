# Backend Test Coverage Queue

Last aligned: 2026-07-03  
Target repo: `ivanjovicic/MathLearning`

## Purpose

Increase backend confidence by risk, not by chasing superficial coverage percentage.

Coverage audit: [`../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`](../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md)  
New prompt-ready gaps: [`backend_test_followups_2026_07_03.md`](backend_test_followups_2026_07_03.md)

## Read first

- `../../AGENTS.md`
- `../BACKEND_TEST_COVERAGE_STRATEGY.md`
- `../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`
- `../BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md`
- `../BACKEND_REGRESSION_GUARDRAILS.md`
- `../AGENT_RUN_LOG_ENFORCEMENT.md`
- `../ai/learning/MISTAKE_LEDGER.md`

## Rules

- One critical flow per prompt.
- Add the smallest tests that prove the invariant.
- Prefer endpoint/integration tests for auth and contract behavior.
- Use SQLite/PostgreSQL for relational guarantees.
- Record exact validation or why it did not run.
- Do not mark Done without `.ai/runs` evidence.
- A committed test remains **Implemented / Needs validation** until `dotnet test` or checked CI evidence exists.
- Failure-injection tests for transaction atomicity should fail after SQL is issued but before commit when that branch matters.
- Explicit anonymous tests must use `X-Test-Anonymous: true`; no-header requests are authenticated by the test handler for compatibility.
- Do not set arbitrary coverage thresholds before a successful baseline artifact is reviewed.

## Active prompts

| ID | Status | Purpose |
|---|---|---|
| BACKEND-TEST-CORE-001 | Needs validation | Daily Run/cosmetics trust boundary, economy state machine, refresh-token primitives, relational constraints and initial CI coverage collection. Run log: `.ai/runs/2026-07-02-BACKEND-TEST-CORE-001-evidence.md`. |
| BACKEND-TEST-002 | Covered / Needs validation | Season settlement response snapshot and exact replay-body truth. |
| BACKEND-TEST-003 | Implemented / Needs validation | Operation identity helper/HTTP characterization and empty offline-session replay behavior. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-003-evidence.md`. |
| BACKEND-TEST-004 | Validated | Offline timestamp UTC normalization, future/old/malformed bounds and equivalent timestamp dedupe: 25 passed. |
| BACKEND-TEST-005 | Validated | Safe auth/global error responses: 41 passed; focused safe-error subset 6 passed. |
| BACKEND-TEST-006 | Validated / anonymous branch strengthened | Monitoring/log authorization, redaction and bounds: 9 passed previously; explicit anonymous mode added in 2026-07-03 audit and needs rerun. |
| BACKEND-TEST-007 | Validated | Public identity allowlists: 10 passed. |
| BACKEND-TEST-008 | Validated | Avatar upload/static-serving safety: 43 passed. |
| BACKEND-TEST-009 | Implemented / Needs validation | Relational uniqueness, rollback and deterministic two-context duplicate-insert recovery. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-009-evidence.md`. |
| BACKEND-TEST-010 | Validated | Read bounds and enum normalization: 70 passed. |
| BACKEND-TEST-011 | Implemented / Workflow validation needed | CI now publishes ReportGenerator Markdown/HTML/Cobertura coverage summary; baseline and thresholds remain pending. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-011-evidence.md`. |
| BACKEND-TEST-012 | Confirmed drift / Needs safe patch | Refresh-token generator emits 88 chars while EF model/snapshot declare 64 and migration history says 128. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-012-evidence.md`. |
| BACKEND-TEST-013 | Ready / P0 contract decision | Require, gate or explicitly isolate missing operation identity for quiz/SRS/offline mutation paths. |
| BACKEND-TEST-014 | Implemented / Needs validation | Shared/cosmetics idempotency state machines and canonical payload semantics: 30 new scenarios. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-014-evidence.md`. |
| BACKEND-TEST-015 | Implemented / Needs validation | Real HTTP refresh-token race against file-backed SQLite. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-015-evidence.md`. |
| BACKEND-TEST-016 | Implemented / Needs validation | Transaction-helper commit, rollback-after-SQL, retry, exhaustion and cancellation. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-016-evidence.md`. |
| BACKEND-TEST-017 | Implemented / Needs validation | Relational mobile-registration rollback and retry without orphan state/double welcome grant. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-017-evidence.md`. |
| BACKEND-TEST-018 | Implemented / Needs validation | Offline-submit relational rollback, retry and replay. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-018-evidence.md`. |
| BACKEND-TEST-019 | Implemented / Needs validation | Direct relational QuizAttempt ingest aggregation, rollback, cancellation and scheduler-order tests. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-019-evidence.md`. |
| BACKEND-TEST-020 | Runtime-fixed / Needs validation | Bug report submission is authenticated and global list/detail/update routes require admin policy; four endpoint authorization tests. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-020-evidence.md`. |
| BACKEND-TEST-021 | Runtime-fixed / Needs validation | Maintenance routes require admin policy; HTTP denial and endpoint-metadata tests. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-021-evidence.md`. |
| BACKEND-TEST-022…035 | Prompt-ready | Durable ingest, outbox concurrency, maintenance semantics, bug input/storage, public diagnostics, dead routes, pagination, analytics/explanations, scheduler, PostgreSQL, cancellation, aliases and auth-test audit. See `backend_test_followups_2026_07_03.md`. |

## Existing validated coverage — do not duplicate blindly

| Area | Evidence |
|---|---|
| Offline timestamps | `.ai/runs/2026-07-01-BACKEND-CRIT-007-evidence.md` — 25 passed. |
| Safe errors | `.ai/runs/2026-06-24-BACKEND-CRIT-001-evidence.md` — 41 passed; focused subset 6 passed. |
| Monitoring/log security | `.ai/runs/2026-06-24-BACKEND-CRIT-002-evidence.md` — 9 passed before explicit-anonymous infrastructure change. |
| Public identity | `.ai/runs/2026-07-01-BACKEND-CRIT-003-evidence.md` — 10 passed. |
| Avatar safety | `.ai/runs/2026-06-24-BACKEND-CRIT-004-evidence.md` — 43 passed. |
| Bounded reads | `.ai/runs/2026-07-01-BACKEND-CRIT-008-evidence.md` — 70 passed. |
| Proxy/authoring/adaptive/jobs/admin seed/version race | `.ai/runs/2026-06-24-BACKEND2-*` evidence — 82 targeted tests. |

## Implemented package details

### BACKEND-TEST-002 — Settlement snapshot truth

Existing coverage confirms:

- first season Daily Run response includes newly awarded XP;
- first milestone response includes the new claim/reward;
- exact retry replays the same authoritative body;
- tracked progress prevents no-tracking snapshot omissions.

Evidence:

- `tests/MathLearning.Tests/Endpoints/EconomySettlementEndpointsIntegrationTests.cs`
- `tests/MathLearning.Tests/Contracts/MobileEconomyContractIntegrationTests.cs`

Validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~Season"
```

### BACKEND-TEST-003 — Operation identity characterization

Implemented:

- missing, whitespace, single and distinct quiz/SRS identity-field helper branches;
- single-key promotion to both ledger dimensions;
- exact replay settles once;
- missing-key legacy non-ledger behavior;
- empty offline-session identity creates independent session rows despite answer/XP dedupe.

Validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~OperationIdentity"
```

Follow-up: BACKEND-TEST-013 must choose strict rejection, version/header gating or explicit legacy-route isolation.

### BACKEND-TEST-009 — Relational idempotency guarantees

Implemented:

- shared-ledger and economy transaction/domain rollback;
- deterministic two-context duplicate-insert recovery;
- single persisted row and winner replay;
- relational unique-scope constraints and user isolation.

Validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~RelationalIdempotency"
```

PostgreSQL remains the authority for serialization semantics.

### BACKEND-TEST-011 — Coverage visibility

Implemented in `.github/workflows/database-validation.yml`:

- raw TRX, Cobertura and JSON collection retained;
- ReportGenerator Markdown summary appended to GitHub job summary;
- HTML and merged Cobertura report uploaded;
- no blocking threshold until a successful stable baseline is reviewed.

Workflow validation required on `main`/PR.

### BACKEND-TEST-012 — Refresh-token model drift

Confirmed:

- generated token length: 88;
- migration target: 128;
- current runtime model/snapshot: 64.

Required safe local patch:

- change fluent model and snapshot to 128;
- do not create a redundant widening migration;
- add metadata and relational persistence regression tests;
- run pending-model-change and schema-from-zero validation.

### BACKEND-TEST-014 — Direct idempotency state machines

Implemented 30 scenarios across:

- `IdempotencyLedgerServiceTests`;
- `CosmeticsIdempotencyServiceTests`;
- `IdempotencyPayloadCanonicalizerTests`.

Validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~IdempotencyLedgerServiceTests|FullyQualifiedName~CosmeticsIdempotencyServiceTests|FullyQualifiedName~IdempotencyPayloadCanonicalizerTests"
```

### BACKEND-TEST-015 — Relational refresh rotation

Implemented real login/refresh HTTP race with two request scopes, deterministic save coordination, one success, one unauthorized result and no losing third token.

Validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~AuthRefreshRelationalConcurrencyTests"
```

### BACKEND-TEST-016 — Transaction helper semantics

Implemented:

- durable success;
- rollback after inner SQL save;
- one conflict then successful clean retry;
- three-conflict exhaustion;
- cancellation without persistence.

Validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~ApiDbTransactionHelpersRelationalTests"
```

### BACKEND-TEST-017 — Registration relational atomicity

Implemented:

- fail after profile SQL and roll back Identity/profile;
- fail after refresh-token SQL and roll back all account state;
- retry creates one user/profile/token and exactly 100 welcome coins;
- internal failure secret is not exposed.

Validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~AuthMobileRegistrationRelationalAtomicityTests|FullyQualifiedName~AuthMobileRegistrationAtomicityTests"
```

### BACKEND-TEST-018 — Offline batch relational rollback

Implemented:

- fail after answer/audit SQL and roll back session, answer, audit, stat, XP and activity state;
- retry awards/imports exactly once;
- exact replay imports zero and keeps authoritative state unchanged.

Validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~OfflineBatchRelationalAtomicityTests|OfflineSubmit|OfflineBatch|OfflineAnswerTimestamp"
```

### BACKEND-TEST-019 — QuizAttempt ingest service

Implemented:

- empty batch no-op;
- durable attempt writes and negative-time normalization;
- topic/subtopic aggregate creation;
- existing aggregate accumulation and monotonic LastAttempt;
- rollback after SQL was issued;
- cancellation with no durable state;
- scheduler invoked only after successful commit.

Files:

- `tests/MathLearning.Tests/Services/QuizAttemptIngestServiceRelationalTests.cs`
- `.ai/runs/2026-07-03-BACKEND-TEST-019-evidence.md`

Validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~QuizAttemptIngestServiceRelationalTests"
```

Important: this does not fix the post-authoritative-commit delivery gap. BACKEND-TEST-022 owns durable delivery/idempotent recovery.

### BACKEND-TEST-020 — Bug endpoint authorization

Runtime fix:

- `/api/bugs/report` and `/api/bugs/mine` explicitly require authentication;
- global list/detail/update routes require `UiTokensAdminPolicy`;
- ordinary learners cannot invoke admin service methods.

Tests:

- explicit anonymous denial;
- learner report/mine ownership and page normalization;
- learner denial for list/detail/update;
- admin success and bounded paging.

Validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~BugEndpointAuthorizationTests"
```

### BACKEND-TEST-021 — Maintenance authorization

Runtime fix:

- every `/api/maintenance/*` route requires `UiTokensAdminPolicy`.

Tests:

- explicit anonymous denial for all three routes;
- authenticated learner receives 403;
- route metadata contains the exact admin policy.

Validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~MaintenanceEndpointAuthorizationTests"
```

Positive admin behavior and GET side-effect removal remain in BACKEND-TEST-024.

## Combined validation for the 2026-07-03 audit package

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~QuizAttemptIngestServiceRelationalTests|FullyQualifiedName~BugEndpointAuthorizationTests|FullyQualifiedName~MaintenanceEndpointAuthorizationTests|FullyQualifiedName~MonitoringLogAuthorizationTests"

dotnet build MathLearning.slnx -c Release
```

Then run the full suite with coverage:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --logger "trx;LogFileName=mathlearning-tests.trx" --results-directory artifacts/test-results --collect:"XPlat Code Coverage" --settings tests/MathLearning.Tests/coverage.runsettings
```

## Next execution order

1. Execute and repair all Implemented / Needs validation packages.
2. Apply BACKEND-TEST-012 refresh-token model/snapshot correction.
3. Run BACKEND-TEST-022 durable ingest delivery.
4. Run BACKEND-TEST-023 outbox multi-instance safety.
5. Run BACKEND-TEST-032 PostgreSQL provider-specific lane.
6. Resolve BACKEND-TEST-013 operation-identity contract with mobile sync.
7. Continue BACKEND-TEST-024…031 and 033…035 by risk order.
8. Review the first coverage summary and only then define progressive thresholds.

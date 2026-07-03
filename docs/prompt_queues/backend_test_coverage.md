# Backend Test Coverage Queue

Last aligned: 2026-07-03  
Target repo: `ivanjovicic/MathLearning`

## Purpose

Increase backend confidence by risk, not by chasing superficial coverage percentage.

Coverage audit: [`../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`](../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md)  
Prompt-ready architecture gaps: [`backend_test_followups_2026_07_03.md`](backend_test_followups_2026_07_03.md)

## Read first

- `../../AGENTS.md`
- `../BACKEND_TEST_COVERAGE_STRATEGY.md`
- `../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`
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
- Failure-injection tests should fail after SQL is issued but before commit when transaction rollback is the invariant.
- Explicit anonymous tests must use `X-Test-Anonymous: true`.
- Do not set arbitrary coverage thresholds before a successful baseline artifact is reviewed.
- Content normalization/sanitization changes require preservation, idempotence and security tests.

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
| BACKEND-TEST-022…035 | Prompt-ready | Durable ingest, outbox concurrency, maintenance semantics, bug storage, public diagnostics, dead routes, pagination, analytics/explanations, scheduler, PostgreSQL, cancellation, aliases and auth-test audit. |
| BACKEND-TEST-036 | Runtime-fixed + tests / Needs validation | Previously unprompted core gaps: identity mapping, observability, startup/schema decisions, weakness math, LaTeX preservation, content sanitization, step generation, translation fallback and question invariants. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-036-evidence.md`. |

## Existing validated coverage — do not duplicate blindly

| Area | Evidence |
|---|---|
| Offline timestamps | `.ai/runs/2026-07-01-BACKEND-CRIT-007-evidence.md` — 25 passed. |
| Safe errors | `.ai/runs/2026-06-24-BACKEND-CRIT-001-evidence.md` — 41 passed; focused subset 6 passed. |
| Monitoring/log security | `.ai/runs/2026-06-24-BACKEND-CRIT-002-evidence.md` — 9 passed before explicit-anonymous infrastructure update. |
| Public identity | `.ai/runs/2026-07-01-BACKEND-CRIT-003-evidence.md` — 10 passed. |
| Avatar safety | `.ai/runs/2026-06-24-BACKEND-CRIT-004-evidence.md` — 43 passed. |
| Bounded reads | `.ai/runs/2026-07-01-BACKEND-CRIT-008-evidence.md` — 70 passed. |
| Proxy/authoring/adaptive/jobs/admin seed/version race | `.ai/runs/2026-06-24-BACKEND2-*` evidence — 82 targeted tests. |

## Key package validation

### Settlement and operation identity

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~Season|FullyQualifiedName~OperationIdentity"
```

BACKEND-TEST-013 remains responsible for the missing-identity contract decision.

### Relational idempotency, refresh, registration, offline and helper transactions

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~RelationalIdempotency|FullyQualifiedName~AuthRefreshRelationalConcurrencyTests|FullyQualifiedName~AuthMobileRegistrationRelationalAtomicityTests|FullyQualifiedName~OfflineBatchRelationalAtomicityTests|FullyQualifiedName~ApiDbTransactionHelpersRelationalTests"
```

SQLite validates relational rollback and local races; PostgreSQL remains authoritative for provider-specific serialization/locking under BACKEND-TEST-032.

### Direct idempotency state machines

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~IdempotencyLedgerServiceTests|FullyQualifiedName~CosmeticsIdempotencyServiceTests|FullyQualifiedName~IdempotencyPayloadCanonicalizerTests"
```

### Ingest, bug and maintenance packages

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~QuizAttemptIngestServiceRelationalTests|FullyQualifiedName~BugEndpointAuthorizationTests|FullyQualifiedName~MaintenanceEndpointAuthorizationTests|FullyQualifiedName~MonitoringLogAuthorizationTests"
```

Durable post-authoritative-commit ingest delivery remains BACKEND-TEST-022. Positive/read-only maintenance behavior remains BACKEND-TEST-024.

## BACKEND-TEST-036 — Previously unprompted high-value coverage

Run mode: pure unit tests + endpoint contracts + focused runtime fixes  
Evidence: `.ai/runs/2026-07-03-BACKEND-TEST-036-evidence.md`

Implemented test areas:

- deterministic `UserIdGuidMapper` behavior, exact SHA-256 mapping, invalid input and 1,000-value isolation;
- `IdempotencyObservabilityService` totals, normalization, sorting, reset, canonical endpoint mapping, privacy-safe logging and 20,000 parallel increments;
- idempotency observability endpoint anonymous/learner/admin authorization, response privacy and exact policy metadata;
- database startup-mode configuration/environment fallbacks, schema status factories/state replacement and deployment/local mismatch guidance;
- complete `WeaknessScoring` thresholds, rounding, clamping, recency, confidence, slow-solve boost and P95 boundaries;
- `InlineLatexFormatter` preservation, wrapping, mixed-content and idempotence tests;
- real `/api/quiz/questions` HTTP contract preserving stored inline math in text/options/hints/explanation;
- `StepEngine` stored-step precedence, translations, arithmetic/equation generation, localization and fallback behavior;
- `MathContentSanitizer` scripts, event handlers, dangerous URLs, HTML/plain modes, malformed LaTeX, delimiters and semantics text;
- `TranslationHelper` requested language, English/original fallback, hints/options and accessibility semantics;
- `Question` domain text/difficulty/answer/hint/publish/version/delete invariants.

Runtime fixes found by tests:

1. Existing `$...$` expressions were removed because `Regex.Split` discarded non-captured delimiters. Formatter now copies existing inline math byte-for-byte and normalizes only plain segments.
2. HTML sanitizer removed only quoted event handlers and allowed dangerous `javascript:`/`data:` URL attributes. It now handles quoted/unquoted event values and unsafe `href`/`src`, while preserving safe HTTP URLs.

Focused validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~UserIdGuidMapperTests|FullyQualifiedName~IdempotencyObservabilityServiceTests|FullyQualifiedName~IdempotencyObservabilityAuthorizationTests|FullyQualifiedName~DatabaseSchemaVersionGuardTests|FullyQualifiedName~WeaknessScoringTests|FullyQualifiedName~InlineLatexFormatterTests|FullyQualifiedName~InlineLatexEndpointContractTests|FullyQualifiedName~StepEngineTests|FullyQualifiedName~MathContentSanitizerTests|FullyQualifiedName~TranslationHelperTests|FullyQualifiedName~QuestionEntityTests"

dotnet build MathLearning.slnx -c Release
```

Related broader regression command:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Translation|QuestionAuthoring|Explanation|QuizEndpoint|Srs|IdempotencyObservability|DatabaseSchema|Weakness"
```

Do not move BACKEND-TEST-036 to Validated until the focused command and release build pass.

## Coverage workflow

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --logger "trx;LogFileName=mathlearning-tests.trx" --results-directory artifacts/test-results --collect:"XPlat Code Coverage" --settings tests/MathLearning.Tests/coverage.runsettings
```

The first successful ReportGenerator artifact must be reviewed before setting line/branch thresholds.

## Next execution order

1. Execute BACKEND-TEST-036 and repair any compile/assertion failures.
2. Execute all other Implemented / Needs validation packages.
3. Apply BACKEND-TEST-012 refresh-token model/snapshot correction.
4. Run BACKEND-TEST-022 durable ingest delivery.
5. Run BACKEND-TEST-023 outbox multi-instance safety.
6. Run BACKEND-TEST-032 PostgreSQL provider-specific lane.
7. Resolve BACKEND-TEST-013 operation-identity contract with mobile sync.
8. Continue BACKEND-TEST-024…031 and 033…035 by risk order.
9. Review the first coverage summary and introduce progressive thresholds only from measured evidence.

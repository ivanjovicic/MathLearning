# Backend Test Coverage Queue

Last aligned: 2026-07-03  
Target repo: `ivanjovicic/MathLearning`

## Purpose

Increase backend confidence by risk, not by chasing superficial coverage percentage.

## Read first

- `../../AGENTS.md`
- `../BACKEND_TEST_COVERAGE_STRATEGY.md`
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
- Reconcile this queue before adding tests; several earlier Ready rows were already covered by validated BACKEND-CRIT packages.

## Active prompts

| ID | Status | Purpose |
|---|---|---|
| BACKEND-TEST-CORE-001 | Needs validation | Daily Run/cosmetics trust boundary, economy state machine, refresh-token primitives, relational constraints, CI coverage artifacts. Run log: `.ai/runs/2026-07-02-BACKEND-TEST-CORE-001-evidence.md`. |
| BACKEND-TEST-002 | Covered / Needs validation | Settlement snapshot truth is already asserted in endpoint and mobile-contract season tests; avoid duplicate tests unless a distinct transaction/concurrency gap is found. |
| BACKEND-TEST-003 | Implemented / Needs validation | Single-key promotion, missing-key legacy behavior, and empty offline-session replay characterization. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-003-evidence.md`. |
| BACKEND-TEST-004 | Validated | Offline timestamp UTC normalization, future/old/malformed bounds, and equivalent timestamp dedupe. Existing focused result: 25 passed, 0 failed. |
| BACKEND-TEST-005 | Validated | Safe auth/global error responses. Existing result: 41 passed, 0 failed; focused safe-error subset 6 passed, 0 failed. |
| BACKEND-TEST-006 | Validated | Monitoring/log authorization, redaction, and bounds. Existing result: 9 passed, 0 failed. |
| BACKEND-TEST-007 | Validated | Public identity allowlists for search/profile/leaderboard surfaces. Existing result: 10 passed, 0 failed. |
| BACKEND-TEST-008 | Validated | Avatar upload size/type/content checks, path safety, cross-user denial, and static-file bypass prevention. Existing result: 43 passed, 0 failed. |
| BACKEND-TEST-009 | Implemented / Needs validation | SQLite unique constraints plus new transactional rollback and deterministic two-context duplicate-insert recovery tests. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-009-evidence.md`. |
| BACKEND-TEST-010 | Validated | Read bounds and enum normalization for search, leaderboard, history, logs, and monitoring. Existing result: 70 passed, 0 failed. |
| BACKEND-TEST-011 | Ready after coverage artifact | Measure baseline and propose progressive line/branch thresholds. |
| BACKEND-TEST-012 | Confirmed drift / Needs safe patch | RefreshToken model/snapshot max length is 64 while generator emits 88 chars and migration history widened the column to 128. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-012-evidence.md`. |
| BACKEND-TEST-013 | Ready / P0 decision | Decide and enforce required operation identity for retryable quiz/SRS/offline mutations while keeping any intentional legacy compatibility explicit and bounded. |
| BACKEND-TEST-014 | Implemented / Needs validation | Direct shared/cosmetics idempotency service state machines and canonical payload semantics; 30 new test scenarios. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-014-evidence.md`. |

## BACKEND-TEST-002 — Settlement snapshot truth

Run mode: tests/bugfix if required  
Token budget: medium

Existing coverage confirmed:

- first season Daily Run response includes newly awarded XP;
- first milestone response includes newly claimed milestone and reward;
- exact retry replays the same authoritative response;
- tracked progress and additional claimed milestone IDs prevent no-tracking snapshot omissions.

Evidence files:

- `tests/MathLearning.Tests/Endpoints/EconomySettlementEndpointsIntegrationTests.cs`
- `tests/MathLearning.Tests/Contracts/MobileEconomyContractIntegrationTests.cs`

Validation still required:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~Season"
```

## BACKEND-TEST-003 — Required P0 operation identity characterization

Run mode: tests/investigation  
Token budget: medium

Implemented:

- helper branch tests for missing, whitespace, single, and distinct quiz/SRS identity fields;
- HTTP tests proving a single `operationId` or `idempotencyKey` is promoted to both ledger dimensions;
- replay tests proving single-key retries settle once;
- HTTP characterization that quiz/SRS requests with no identity still use legacy non-ledger paths;
- offline characterization proving an empty session ID does not duplicate the answer/XP mutation, but creates independent session rows per replay.

Evidence files:

- `tests/MathLearning.Tests/Endpoints/OperationIdentityResolutionTests.cs`
- `tests/MathLearning.Tests/Contracts/OperationIdentityContractIntegrationTests.cs`
- `.ai/runs/2026-07-03-BACKEND-TEST-003-evidence.md`

Validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~OperationIdentity"
```

Follow-up decision:

- BACKEND-TEST-013 must decide whether missing identity is rejected, version-gated, or retained only on an explicit legacy route.

## BACKEND-TEST-004…008 and 010 — Existing validated coverage

Do not add duplicate tests unless a new branch or endpoint is identified.

Evidence:

- offline timestamps: `.ai/runs/2026-07-01-BACKEND-CRIT-007-evidence.md` — 25 passed;
- safe error responses: `.ai/runs/2026-06-24-BACKEND-CRIT-001-evidence.md` — 41 passed, focused subset 6 passed;
- monitoring/log security: `.ai/runs/2026-06-24-BACKEND-CRIT-002-evidence.md` — 9 passed;
- public identity minimization: `.ai/runs/2026-07-01-BACKEND-CRIT-003-evidence.md` — 10 passed;
- avatar safety: `.ai/runs/2026-06-24-BACKEND-CRIT-004-evidence.md` — 43 passed;
- bounded reads: `.ai/runs/2026-07-01-BACKEND-CRIT-008-evidence.md` — 70 passed.

## BACKEND-TEST-009 — Relational idempotency guarantees

Run mode: tests  
Token budget: medium

Previously implemented:

- unique user/type/idempotency-key scope;
- unique user/type/operation-id scope;
- multiple null operation IDs with different keys;
- Daily Run user/day uniqueness;
- Daily Run user/transaction uniqueness;
- cosmetics user/operation and user/key uniqueness;
- different-user isolation.

Added in this package:

- shared-ledger completion and domain mutation roll back atomically;
- economy completion and balance mutation roll back atomically;
- deterministic two-context shared-ledger race: both requests query before either insert, one inserts, the second hits uniqueness and reloads the same pending ledger;
- deterministic two-context economy race with the same invariant;
- winner completion is replayed from the single persisted row.

Evidence files:

- `tests/MathLearning.Tests/Idempotency/RelationalIdempotencyConstraintTests.cs`
- `tests/MathLearning.Tests/Idempotency/RelationalIdempotencyTransactionTests.cs`
- `.ai/runs/2026-07-03-BACKEND-TEST-009-evidence.md`

Validation required:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~RelationalIdempotency"
```

PostgreSQL follow-up:

- run the same test project in `database-validation.yml` against PostgreSQL schema-from-zero;
- do not describe SQLite as proof of PostgreSQL serialization semantics.

## BACKEND-TEST-012 — Refresh-token model length drift

Run mode: bugfix + tests  
Token budget: low

Confirmed evidence:

- `RefreshTokenService.GenerateRefreshToken()` emits Base64 for 64 random bytes: 88 characters;
- migration `20260210114958_IncreaseRefreshTokenLength` changed the DB column to 128;
- current `ApiDbContext` and `ApiDbContextModelSnapshot` still declare max length 64.

Required safe patch:

- change `RefreshToken.Token` fluent max length from 64 to 128;
- change the current model snapshot max length/column type from 64 to 128;
- do not create a redundant migration;
- add a model metadata regression test proving generated token length fits the configured maximum;
- add a relational persistence test for a generated token;
- run schema-from-zero validation and refresh-token tests.

Connector limitation recorded in the run log: the current GitHub contents action only supports complete-file replacement, while the two EF files are approximately 1,600 and 5,800 lines. A local targeted patch is safer than rewriting either file through the connector.

## BACKEND-TEST-013 — Enforce or explicitly bound missing operation identity

Run mode: contract decision + implementation + tests  
Token budget: medium

Current characterized behavior:

- quiz answer and SRS update without `operationId`/`idempotencyKey` bypass the ledger;
- exact client retries can therefore re-enter mutable legacy paths;
- offline submit with an empty session ID deduplicates by answer timestamp but creates a new quiz-session row on each replay.

Required decision and proof:

- choose strict rejection, version/header gating, or explicit legacy-route compatibility;
- canonical mobile routes must require stable operation identity after the compatibility decision;
- empty offline session identity must not create unbounded session rows;
- add positive, replay, rejection, and legacy compatibility tests;
- synchronize backend and mobile contract docs if behavior changes.

## BACKEND-TEST-014 — Direct idempotency service state machines

Run mode: tests  
Token budget: medium

Implemented:

- shared `IdempotencyLedgerService` first-process, completed replay, failed replay, canonical payload equivalence, payload conflict, dual-key collision, user/type isolation, illegal transitions, missing-ledger and required-scope tests;
- `CosmeticsIdempotencyService` first-process, completed replay, failed replay, canonical payload equivalence, payload conflict, dual-key collision, user isolation, illegal transitions, missing-ledger and required-scope tests;
- canonical JSON/hash tests for recursive ordering, equivalent object order, array-order significance, web naming, `JsonElement`, null/primitives, serialization and SHA-256 stability.

Evidence files:

- `tests/MathLearning.Tests/Services/IdempotencyLedgerServiceTests.cs`
- `tests/MathLearning.Tests/Services/CosmeticsIdempotencyServiceTests.cs`
- `tests/MathLearning.Tests/Services/IdempotencyPayloadCanonicalizerTests.cs`
- `.ai/runs/2026-07-03-BACKEND-TEST-014-evidence.md`

Validation required:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~IdempotencyLedgerServiceTests|FullyQualifiedName~CosmeticsIdempotencyServiceTests|FullyQualifiedName~IdempotencyPayloadCanonicalizerTests"
```

Do not move to Done until the focused command passes and the result is recorded in the run log. Follow with relational concurrency coverage under BACKEND-TEST-009 rather than treating EF InMemory as proof of database uniqueness.

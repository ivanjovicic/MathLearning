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

## Active prompts

| ID | Status | Purpose |
|---|---|---|
| BACKEND-TEST-CORE-001 | Needs validation | Daily Run/cosmetics trust boundary, economy state machine, refresh-token primitives, relational constraints, CI coverage artifacts. Run log: `.ai/runs/2026-07-02-BACKEND-TEST-CORE-001-evidence.md`. |
| BACKEND-TEST-002 | Covered / Needs validation | Settlement snapshot truth is already asserted in endpoint and mobile-contract season tests; avoid adding duplicate tests unless a distinct transaction/concurrency gap is found. |
| BACKEND-TEST-003 | Implemented / Needs validation | Single-key promotion, missing-key legacy behavior, and empty offline-session replay characterization. Run log: `.ai/runs/2026-07-03-BACKEND-TEST-003-evidence.md`. |
| BACKEND-TEST-004 | Ready | Offline timestamp UTC normalization, future/old/malformed bounds, equivalent timestamp dedupe. |
| BACKEND-TEST-005 | Ready | Safe error responses: no raw exception details in auth/global middleware responses. |
| BACKEND-TEST-006 | Ready | Monitoring/log authorization and redaction for anonymous and non-admin callers. |
| BACKEND-TEST-007 | Ready | Public identity allowlists across search/profile/leaderboard/rivals/school surfaces. |
| BACKEND-TEST-008 | Ready | Avatar upload size/type/content checks and static-file access policy. |
| BACKEND-TEST-009 | Partial / Needs validation | SQLite unique-index tests added; transaction rollback and true concurrent settlement cases remain. |
| BACKEND-TEST-010 | Ready | Read bounds and enum validation for search, leaderboard, history, and monitoring endpoints. |
| BACKEND-TEST-011 | Ready after coverage artifact | Measure baseline and propose progressive line/branch thresholds. |
| BACKEND-TEST-012 | Ready / P0-P1 | Repair RefreshToken model/snapshot max length drift (64 vs existing 128 migration), add model metadata regression test, and verify schema-from-zero. |
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

## BACKEND-TEST-003 — Required P0 operation identity

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

## BACKEND-TEST-004 — Offline timestamp boundaries

Run mode: tests  
Token budget: medium

Cover:

- valid UTC timestamp;
- offset timestamp normalized to UTC;
- malformed timestamp;
- future timestamp beyond tolerance;
- excessively old timestamp;
- equivalent timestamps do not bypass dedupe.

## BACKEND-TEST-009 — Relational idempotency guarantees

Run mode: tests  
Token budget: medium

Implemented in this batch:

- unique user/type/idempotency-key scope;
- unique user/type/operation-id scope;
- multiple null operation IDs with different keys;
- Daily Run user/day uniqueness;
- Daily Run user/transaction uniqueness;
- cosmetics user/operation and user/key uniqueness;
- different-user isolation.

Still required:

- rollback does not leave completed ledger state;
- true concurrent duplicate requests settle once against a relational provider.

## BACKEND-TEST-012 — Refresh-token model length drift

Run mode: bugfix + tests  
Token budget: low

Evidence:

- `RefreshTokenService.GenerateRefreshToken()` emits Base64 for 64 random bytes (88 characters).
- migration `20260210114958_IncreaseRefreshTokenLength` changed the DB column to 128.
- current EF configuration and model snapshot declare max length 64.

Required:

- align EF configuration and model snapshot to 128 without creating a redundant shrink/expand migration;
- add a model metadata test proving generated token length fits the configured maximum;
- run schema-from-zero validation and refresh-token tests;
- add/update a `BACKEND-MISTAKE-AUTH-*` or `BACKEND-MISTAKE-MIGRATION-*` card.

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

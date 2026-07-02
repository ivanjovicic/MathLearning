# Backend Test Coverage Queue

Last aligned: 2026-07-02  
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
| BACKEND-TEST-CORE-001 | Needs validation | Daily Run/cosmetics trust boundary, economy state machine, refresh-token primitives, CI coverage artifacts. |
| BACKEND-TEST-002 | Ready | Settlement snapshot truth: first response and replay include newly persisted season/milestone state. |
| BACKEND-TEST-003 | Ready | Required operation identity for P0 quiz/offline/SRS mutations and safe legacy behavior. |
| BACKEND-TEST-004 | Ready | Offline timestamp UTC normalization, future/old/malformed bounds, equivalent timestamp dedupe. |
| BACKEND-TEST-005 | Ready | Safe error responses: no raw exception details in auth/global middleware responses. |
| BACKEND-TEST-006 | Ready | Monitoring/log authorization and redaction for anonymous and non-admin callers. |
| BACKEND-TEST-007 | Ready | Public identity allowlists across search/profile/leaderboard/rivals/school surfaces. |
| BACKEND-TEST-008 | Ready | Avatar upload size/type/content checks and static-file access policy. |
| BACKEND-TEST-009 | Ready | SQLite relational tests for unique idempotency indexes and transaction rollback. |
| BACKEND-TEST-010 | Ready | Read bounds and enum validation for search, leaderboard, history, and monitoring endpoints. |
| BACKEND-TEST-011 | Ready after coverage artifact | Measure baseline and propose progressive line/branch thresholds. |

## BACKEND-TEST-002 — Settlement snapshot truth

Run mode: tests/bugfix if required  
Token budget: medium

Prove:

- first season Daily Run response includes newly awarded XP;
- first milestone response includes newly claimed milestone and reward;
- exact retry replays the same authoritative response;
- no-tracking reads cannot omit pending settlement state.

Validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~Season"
```

## BACKEND-TEST-003 — Required P0 operation identity

Run mode: tests/investigation first  
Token budget: medium

Prove how retryable quiz answer, SRS, and offline submit behave when operation identity is missing. Add a follow-up implementation prompt if current compatibility behavior is unsafe.

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

Use SQLite or PostgreSQL-backed tests for:

- unique user/type/idempotency-key scope;
- unique user/type/operation-id scope;
- Daily Run user/day uniqueness;
- rollback does not leave completed ledger state;
- concurrent duplicate requests settle once.

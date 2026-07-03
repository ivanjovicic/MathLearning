# Backend Test Coverage Audit — 2026-07-03

> This document separates **validated test evidence**, **implemented but not yet executed tests**, and **static findings/prompt-ready gaps**. A static finding is not a runtime fix. A committed test is not validated until `dotnet test` or a checked GitHub Actions run proves it.

Status: active coverage audit and implementation pass  
Repo: `ivanjovicic/MathLearning`  
Primary queue: `docs/prompt_queues/backend_test_coverage.md`  
Follow-up prompts: `docs/prompt_queues/backend_test_followups_2026_07_03.md`

## Executive verdict

The backend has broad test inventory and unusually strong coverage around mobile-authoritative mutations, idempotency, public identity, avatar safety, read bounds, question authoring, rate limiting, and several background jobs. The strongest areas test business invariants rather than only status codes.

The repository still did not expose a trustworthy overall line/branch percentage during this audit. CI collected Cobertura/JSON artifacts but did not publish a summary or threshold. This pass adds a non-blocking ReportGenerator summary; numeric conclusions must wait for the first successful workflow artifact.

The highest remaining risks are not ordinary DTO/property branches. They are cross-transaction delivery, provider-specific concurrency, operational authorization, public operational data, durable background processing, and unbounded pagination/arithmetic.

## Coverage confidence model

| Level | Meaning |
|---|---|
| Validated | A focused/full test result is recorded in existing run evidence. |
| Implemented / needs validation | Test/runtime code is committed, but no executable evidence was available in this connector session. |
| Characterized | Existing behavior is tested or statically confirmed, but a contract/runtime decision remains. |
| Prompt-ready | Material gap confirmed; implementation requires a focused prompt. |
| Unknown | No focused test or reliable evidence found during this audit. |

## Strong or validated areas

| Area | Confidence | Existing evidence |
|---|---|---|
| Offline timestamp normalization and replay window | Validated | 25 focused tests recorded under BACKEND-CRIT-007. |
| Safe auth/global error responses | Validated | 41 tests; focused safe-error subset 6 tests. |
| Monitoring/log authorization and redaction | Validated, test-auth accuracy improved in this pass | 9 tests; explicit anonymous test mode added. |
| Public identity minimization | Validated | 10 tests. |
| Avatar upload/static serving safety | Validated | 43 tests. |
| Bounded search/leaderboard/history/log reads | Validated | 70 tests. |
| Proxy trust, authoring policy, adaptive bounds, recurring jobs, seed policy, version races | Validated | 82 targeted tests recorded in second-pass evidence. |
| Season settlement snapshots and replay bodies | Covered / validation retained | Economy/mobile-contract integration tests. |
| Quiz/SRS operation identity resolution | Implemented / needs validation | 21 characterization/helper/HTTP scenarios. |
| Shared/cosmetics idempotency state machines | Implemented / needs validation | 30 direct service/canonicalization scenarios. |
| Relational idempotency rollback and duplicate-insert recovery | Implemented / needs validation | SQLite transaction/race tests. |
| Refresh rotation race through real HTTP | Implemented / needs validation | Deterministic two-context SQLite test. |
| Mobile registration relational rollback | Implemented / needs validation | Fail-after-SQL profile/token tests plus safe retry. |
| Offline batch relational rollback/retry/replay | Implemented / needs validation | Fail-after-SQL transaction test. |
| Shared transaction helper semantics | Implemented / needs validation | Commit, rollback-after-SQL, retry, exhaustion, cancellation. |

## New implementation in this audit

### BACKEND-TEST-019 — Quiz attempt ingest service

Added direct SQLite relational coverage for:

- empty input no-op;
- durable `QuizAttempt` writes;
- negative time normalization;
- topic/subtopic totals, correct count, accuracy and last-attempt aggregation;
- accumulation into existing stats without moving `LastAttempt` backwards;
- rollback after SQL has already executed;
- scheduler invocation only after successful commit;
- cancellation with no durable mutation.

### BACKEND-TEST-020 — Bug report authorization

Confirmed and fixed:

- bug management list/detail/update routes previously required only generic authentication;
- ordinary learners could potentially read/update reports belonging to other users;
- report group claimed anonymous access while the handler rejected requests without a user claim.

Added tests for anonymous, learner and admin behavior, paging normalization, ownership of `mine`, and proof that denied calls never invoke the service.

### BACKEND-TEST-021 — Maintenance authorization

Confirmed and fixed:

- index rebuild/health/stats routes previously required only generic authentication;
- ordinary learners could potentially trigger index rebuilds or inspect database index details.

Added HTTP denial tests and endpoint metadata tests proving every maintenance route carries the explicit admin policy.

### Test authentication accuracy

`TestAuthHandler` previously authenticated requests without headers as `test-user`. Tests named “anonymous” could therefore be testing an authenticated non-admin path. Added explicit `X-Test-Anonymous: true` support and migrated new security tests plus monitoring anonymous tests to use it.

### Coverage visibility

Updated database-validation CI to:

- retain raw TRX/Cobertura/JSON output;
- generate ReportGenerator HTML and merged Cobertura output;
- publish a Markdown coverage summary to the GitHub job summary;
- upload generated coverage reports;
- remain non-blocking until a stable baseline is observed.

## Highest-priority uncovered or partially covered risks

### P0 — durable quiz-attempt ingest delivery

The offline/quiz transaction commits authoritative answer/XP state before calling `IQuizAttemptIngestService`. If ingest fails after commit, the client can receive an error while retry deduplication produces no new ingest rows. Analytics/weakness state may remain missing permanently.

Required solution: transactional outbox or equivalent durable ingest command, deterministic idempotency key, retry/recovery worker, and fail-after-commit tests.

Prompt: BACKEND-TEST-022.

### P0/P1 — OutboxProcessor multi-instance duplicate publish

`OutboxProcessor` selects unprocessed rows and publishes before marking them processed. There is no visible row claim/lease, `SKIP LOCKED`, ownership token, or compare-and-set. Two instances can select and publish the same message. Poison messages retry indefinitely and `LastError` stores raw exception text.

Prompt: BACKEND-TEST-023.

### P0/P1 — PostgreSQL provider-specific behavior

SQLite tests do not prove:

- PostgreSQL serialization retry;
- named unique-constraint retry;
- `FOR UPDATE` XP locking;
- multi-instance outbox row claiming;
- migration/model consistency.

The workflow provisions PostgreSQL for schema/startup validation, but most application tests still replace the API database with InMemory/SQLite contexts.

Prompt: BACKEND-TEST-032.

### P0/P1 — refresh-token schema/model drift

Open existing BACKEND-TEST-012: generator emits 88 characters, migration widened to 128, runtime EF model and snapshot still declare 64.

### P0 — missing operation identity decision

Open existing BACKEND-TEST-013: canonical quiz/SRS mutations can bypass the ledger when both keys are absent; empty offline session identity can create repeated session rows.

## Additional material gaps

### Maintenance semantics

`GET /api/maintenance/index-stats` calls `RebuildCorruptedIndexesAsync`, so a GET read route may mutate the database. Endpoints instantiate the concrete maintenance service directly, preventing positive admin tests with a fake implementation.

Prompt: BACKEND-TEST-024.

### Bug report service safety

Only severity/status enums are bounded. Screen, description, reproduction steps, platform, locale, version and base64 screenshot size/content are not visibly bounded. Screenshot upload happens before database persistence, so a later DB failure can leave an orphan file.

Prompt: BACKEND-TEST-025.

### Public operational information

Anonymous endpoints currently expose combinations of:

- schema migration names and failure messages;
- pending/unknown migration counts;
- question/category/user counts;
- process memory, thread count and uptime;
- mock monitoring job state.

These may be intentional for platform probes, but the public contract should expose only the minimum readiness signal, with detailed diagnostics behind admin/internal auth.

Prompt: BACKEND-TEST-026.

### Unwired question endpoint family

`QuestionEndpoints.MapQuestionEndpoints` exists but is not registered. If wired later, its `limit` is not clamped and it overlaps the `/api/questions` authoring group. The repo needs an explicit delete/wire/merge decision and route collision tests.

Prompt: BACKEND-TEST-027.

### Pagination overflow and extreme page values

Several endpoints normalize page to at least one but do not cap it. Expressions such as `page * pageSize` and `(page - 1) * pageSize` can overflow for extreme values or generate expensive offsets.

Prompt: BACKEND-TEST-028.

### Analytics/recommendation endpoint contracts

Service/scoring tests exist, but no focused endpoint tests were found for auth-user mapping, paging, empty results, cancellation and prevention of cross-user override.

Prompt: BACKEND-TEST-029.

### Explanation endpoint contracts

No focused endpoint suite was found for auth, validators, cancellation, not-found mapping and safe error bodies. `KeyNotFoundException.Message` is returned directly on two routes.

Prompt: BACKEND-TEST-030.

### Weakness-analysis scheduling durability

The scheduler uses an unbounded in-memory channel. Jobs are lost on restart, duplicate enqueue is not coalesced, and no backpressure/queue-depth contract is tested. The daily service immediately enqueues all active users on each restart.

Prompt: BACKEND-TEST-031.

### Cancellation/rollback matrix for P0 mutations

Some services have cancellation tests, but there is no consistent matrix proving cancellation before save, during transaction, and after ledger creation cannot leave partial state for every canonical P0 mutation.

Prompt: BACKEND-TEST-033.

### Legacy alias parity and retirement

Canonical routes are well documented, but legacy coin/hint/avatar/quiz aliases need a single contract suite proving they either delegate without duplicate side effects or are explicitly deprecated/unavailable.

Prompt: BACKEND-TEST-034.

### Authorization test infrastructure audit

Explicit anonymous mode is now available, but all older authorization tests should be audited for requests that omitted headers and unintentionally authenticated as `test-user`.

Prompt: BACKEND-TEST-035.

### Coverage thresholds

BACKEND-TEST-011 now publishes a summary, but no numeric baseline has yet been observed. Do not set arbitrary thresholds. After a successful run:

1. record overall and assembly/namespace line and branch coverage;
2. set a floor slightly below stable baseline;
3. use stronger gates for auth/idempotency/economy/quiz critical namespaces;
4. add changed-code coverage only after stability.

## Recommended execution order

1. Run all new and previously pending test packages; repair compilation/runtime failures.
2. Fix refresh-token 64/128 drift.
3. Implement durable ingest delivery and idempotent analytics ingestion.
4. Harden OutboxProcessor multi-instance claiming and poison-message handling.
5. Add PostgreSQL provider-specific integration lane.
6. Resolve missing operation identity contract with the mobile repo.
7. Complete maintenance read-only/DI refactor and bug-report input/storage safety.
8. Minimize public health/metrics/monitoring detail.
9. Fill analytics/explanation/scheduler/pagination/cancellation/legacy coverage.
10. Record coverage baseline and then introduce progressive gates.

## Validation status

No `dotnet test`, build, or GitHub Actions result was available during this connector run. Every newly implemented row remains **Implemented / Needs validation**. Exact commands are maintained in the queue and per-prompt run logs.

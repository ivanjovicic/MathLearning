# Backend Test Follow-up Prompt Queue — 2026-07-03

Source audit: `../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`  
Primary queue: `backend_test_coverage.md`  
Target repo: `ivanjovicic/MathLearning`

## Rules

- Do not duplicate BACKEND-TEST-011, 012 or 013.
- Create `.ai/runs/<date>-<prompt-id>-evidence.md` before runtime/test edits.
- Use SQLite/PostgreSQL for transaction, FK, unique-index and concurrency claims.
- A prompt is not Done without executable validation or an explicit validation-failed state.
- Contract changes must record backend/mobile cross-repo sync.
- Prefer one critical invariant per implementation prompt even when this queue groups related required tests.

## Priority table

| ID | Priority | Status | Purpose |
|---|---|---|---|
| BACKEND-TEST-022 | P0 | Prompt-ready | Durable, idempotent delivery of quiz/offline attempts to analytics after authoritative settlement. |
| BACKEND-TEST-023 | P0/P1 | Prompt-ready | Multi-instance-safe outbox claiming, duplicate-publish defense and poison-message lifecycle. |
| BACKEND-TEST-024 | P1 | Prompt-ready | Make maintenance routes testable/read-only where appropriate and add positive admin tests. |
| BACKEND-TEST-025 | P1 | Prompt-ready | Bound bug report input/screenshot handling and prevent orphan screenshot storage. |
| BACKEND-TEST-026 | P1 | Prompt-ready | Minimize public health/metrics/monitoring information while retaining platform probes. |
| BACKEND-TEST-027 | P1/P2 | Prompt-ready | Decide whether to wire, merge or remove dead `QuestionEndpoints`; prevent route/limit drift. |
| BACKEND-TEST-028 | P1/P2 | Prompt-ready | Prevent pagination arithmetic overflow and extreme-offset abuse. |
| BACKEND-TEST-029 | P1 | Prompt-ready | Add analytics/recommendation HTTP contract and user-scope coverage. |
| BACKEND-TEST-030 | P1 | Prompt-ready | Add explanation endpoint validation, safe-error and cancellation coverage. |
| BACKEND-TEST-031 | P1 | Prompt-ready | Make weakness scheduling bounded, deduplicated and restart-safe or document accepted loss. |
| BACKEND-TEST-032 | P0/P1 | Prompt-ready | Run provider-specific PostgreSQL concurrency/locking/constraint integration tests. |
| BACKEND-TEST-033 | P1 | Prompt-ready | Add cancellation and rollback matrix for every canonical P0 mutation. |
| BACKEND-TEST-034 | P1/P2 | Prompt-ready | Prove legacy route parity/deprecation and prevent duplicate settlement surfaces. |
| BACKEND-TEST-035 | P1 | Prompt-ready | Audit all authorization tests for false “anonymous” coverage and migrate to explicit anonymous mode. |

---

## BACKEND-TEST-022 — Durable quiz-attempt ingest delivery

Run mode: runtime architecture + migration + tests  
Risk: P0 progress/analytics data loss and retry inconsistency

### Problem

Quiz/offline answer settlement commits authoritative answer/XP state before `IQuizAttemptIngestService` is called. If ingest fails after commit, the client can receive an error while retry dedupe creates no new ingest rows. Weakness/analytics state can remain permanently incomplete.

### Inspect

- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- `src/MathLearning.Api/Services/QuizAttemptIngestService.cs`
- `src/MathLearning.Application/Services/IQuizAttemptIngestService.cs`
- `src/MathLearning.Domain/Entities/WeaknessAnalysisModels.cs`
- existing `OutboxMessage` infrastructure and contexts
- offline/quiz idempotency and transaction tests

### Required work

1. Choose a durable delivery pattern: same-context outbox, durable ingest command table, or an equivalent transactional handoff.
2. Persist the ingest command in the same transaction as the authoritative answer/audit mutation.
3. Give each attempt a stable natural/idempotency key derived from authoritative answer identity, not a random `QuizAttempt.Id`.
4. Make consumer retries idempotent for attempt rows and topic/subtopic stats.
5. Do not return failure after authoritative settlement merely because asynchronous analytics delivery is pending.
6. Add observability for pending/retried/failed ingest without raw payload leakage.

### Required tests

- failure immediately after authoritative commit still leaves a durable pending ingest command;
- retry/restart processes the command exactly once;
- duplicate consumer delivery does not duplicate `QuizAttempt` or increment stats twice;
- two consumers racing on one command produce one durable effect;
- missing subtopic is diagnosed without endless retry;
- client replay returns authoritative settlement while delivery remains recoverable;
- different users/attempts remain isolated.

### Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "QuizAttemptIngest|OfflineBatch|QuizAnswer|Outbox"
dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext
./scripts/db/validate-schema.ps1
```

Cross-repo sync: required if HTTP success/error semantics change.

---

## BACKEND-TEST-023 — OutboxProcessor multi-instance and poison-message safety

Run mode: refactor + relational concurrency tests  
Risk: P0/P1 duplicate external effects, infinite retry, sensitive error persistence

### Problem

The processor selects all unprocessed rows, publishes, then marks them processed. There is no visible claim/lease or `SKIP LOCKED`. Multiple instances can publish the same row. Failed rows retry forever and store raw exception messages.

### Inspect

- `src/MathLearning.Infrastructure/Services/EventBus/OutboxProcessor.cs`
- `AppDbContext` outbox mapping/migrations
- event bus implementation and consumer idempotency
- background service registration/lifetime

### Required work

1. Extract one-batch processing into a directly testable service.
2. Add atomic claim/lease fields or PostgreSQL `FOR UPDATE SKIP LOCKED` with clear ownership/expiry semantics.
3. Define delivery semantics explicitly: at-least-once plus idempotent consumers, or another justified contract.
4. Add max attempts/backoff/dead-letter state and operator replay rules.
5. Redact/truncate persisted `LastError`.
6. Recover abandoned claims after worker death.

### Required tests

- two processors race; each message is claimed by one worker at a time;
- publish success marks processed only after publish completes;
- publish failure increments attempts and does not mark processed;
- worker death/expired lease allows recovery;
- poison message reaches dead-letter threshold and no longer blocks healthy rows;
- cancellation does not incorrectly mark processed;
- missing-table path stops without log storm;
- error text is redacted and bounded.

### Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Outbox"
```

PostgreSQL execution is mandatory for claim/locking proof.

---

## BACKEND-TEST-024 — Maintenance service DI, read-only semantics and admin success paths

Run mode: refactor + endpoint/service tests  
Risk: P1 operational safety

### Problem

Authorization is now admin-only, but endpoints instantiate `IndexMaintenanceService` directly. `GET /index-stats` calls `RebuildCorruptedIndexesAsync`, so a GET route may mutate database indexes. Positive admin behavior cannot be isolated with a fake service.

### Required work

1. Introduce an injectable maintenance interface.
2. Split read-only health/statistics from mutation/rebuild methods.
3. Ensure every GET route is side-effect free.
4. Add cancellation tokens, audit actor/correlation id and a concurrency/non-overlap guard for rebuild.
5. Return safe summaries; do not expose unnecessary schema/internal SQL details.

### Required tests

- admin can read health/stats through a fake service;
- GET routes never invoke rebuild;
- admin rebuild invokes mutation exactly once;
- ordinary user/anonymous denial remains covered;
- concurrent rebuild requests do not overlap;
- service failure returns a safe response and does not leak connection details;
- cancellation stops work safely.

### Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Maintenance|IndexMaintenance"
```

---

## BACKEND-TEST-025 — Bug report input and screenshot storage safety

Run mode: validators/service transaction tests  
Risk: P1 storage abuse, orphan files, privacy

### Problem

Severity/status are validated, but free-text fields and screenshot size/content are not visibly bounded. Screenshot upload occurs before DB persistence, so DB failure can leave orphan storage.

### Required work

1. Add FluentValidation or equivalent limits for screen, description, steps, platform, locale, version and screenshot encoded/decoded size.
2. Validate supported image type/signature; never trust a client-provided extension.
3. Define screenshot storage compensation: delete on DB failure or use a pending/finalized storage workflow.
4. Normalize status/severity/filter casing.
5. Add rate-limit/abuse policy for report creation.

### Required tests

- empty/oversized fields rejected with no service/storage call;
- invalid/oversized/non-image screenshot rejected;
- DB failure after upload deletes pending screenshot;
- screenshot upload failure creates no bug row;
- `mine` returns only authenticated user's rows;
- admin filters are bounded and normalized;
- invalid status update creates no mutation;
- internal storage errors do not leak.

### Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "BugReport|BugEndpoint"
```

---

## BACKEND-TEST-026 — Public operational surface minimization

Run mode: contract decision + endpoint tests  
Risk: P1 operational information disclosure

### Inspect

- `HealthEndpoints.cs`
- `/metrics` and `/api/monitoring/jobs` in `Program.cs`
- platform health-check configuration
- `HealthEndpointContractTests`

### Required decision

Separate minimal anonymous liveness/readiness from detailed admin/internal diagnostics.

### Required tests

- public liveness/readiness exposes only stable status fields needed by the platform;
- public responses omit migration names, failure messages, user/data counts, thread count and detailed job state;
- admin/internal diagnostic routes retain necessary detail;
- unavailable DB/schema returns safe public reason codes;
- deployment smoke check still succeeds;
- `/api/monitoring/jobs` is either admin-protected or removed until real data exists.

### Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Health|Metrics|Monitoring"
```

Contract/deployment documentation update required.

---

## BACKEND-TEST-027 — QuestionEndpoints wiring/dead-code decision

Run mode: route architecture decision + tests  
Risk: P1/P2 route collision, unbounded read, dead code drift

### Problem

`MapQuestionEndpoints` exists but is not registered. It shares `/api/questions` with authoring routes and uses an unbounded `limit` if later wired.

### Required work

1. Decide one: remove dead endpoint family, merge it into canonical quiz/question reads, or wire it deliberately.
2. If wired, clamp limit, validate id/lang/subtopic, avoid correct-answer leakage beyond intended mobile contract, and check route/name collisions.
3. Update endpoint inventory and mobile docs.

### Required tests

- route presence/absence matches the decision;
- no duplicate route names/pattern conflicts;
- limit negative/zero/extreme values are safe;
- auth and language fallback are correct;
- not-found and empty results are contract-stable;
- response does not expose unintended answer authority.

---

## BACKEND-TEST-028 — Pagination overflow and extreme-offset safety

Run mode: shared helper + endpoint/service tests  
Risk: P1/P2 reliability/performance

### Problem

Several endpoints cap page size but not page. `page * pageSize` and `(page - 1) * pageSize` can overflow or create extreme database offsets.

### Required work

1. Introduce a shared safe pagination normalizer using checked/long arithmetic.
2. Cap page/offset or move high-volume surfaces to cursor pagination.
3. Apply consistently to analytics, recommendations, bug reports and other page-based reads.

### Required tests

- `int.MinValue`, negative, zero, `int.MaxValue` page values;
- extreme pageSize combinations;
- no arithmetic overflow/500;
- service receives bounded take/skip;
- empty far-page response is cheap and stable;
- cancellation propagates to queries.

---

## BACKEND-TEST-029 — Analytics/recommendation HTTP contract coverage

Run mode: endpoint integration tests  
Risk: P1 privacy/user-scope and contract drift

### Required tests

- explicit anonymous request denied;
- authenticated identity is mapped server-side; no query/body user override;
- two users receive isolated analytics;
- page/pageSize normalization and extreme values;
- empty state returns stable arrays/counts;
- service cancellation propagates;
- service exception returns safe error contract;
- response field names/types remain compatible with mobile consumers.

Use a fake `IWeaknessAnalysisService` for endpoint contract tests and retain service integration tests separately.

---

## BACKEND-TEST-030 — Explanation endpoint validation and safe errors

Run mode: endpoint integration tests + small hardening if required  
Risk: P1 error leakage and expensive input

### Required tests

- anonymous denied and user scope cannot be overridden;
- validators reject empty/oversized/problematic math input before service invocation;
- language normalization and problem id bounds;
- not-found maps to stable safe message, not arbitrary exception text;
- cancellation prevents service completion/mutation;
- generated/mistake-analysis response contract is stable;
- rate-limit or cost guard exists for expensive generation routes.

Replace direct `ex.Message` responses if service messages can contain internal details.

---

## BACKEND-TEST-031 — Weakness scheduler durability, deduplication and backpressure

Run mode: architecture decision + scheduler tests  
Risk: P1 lost/duplicated analysis and memory pressure

### Problem

The scheduler uses an unbounded in-memory channel. Jobs are lost on restart, duplicate user ids are not coalesced, and the daily hosted service enqueues active users immediately on every restart.

### Required work

1. Decide whether loss is acceptable. If not, use Hangfire/durable queue/outbox.
2. Bound queue/backpressure and expose safe queue health.
3. Coalesce duplicate user ids while pending/running.
4. Add retry/backoff and shutdown semantics.
5. Inject time for daily scheduling tests.

### Required tests

- duplicate enqueue runs one analysis within a window;
- restart recovery or explicit accepted-loss contract;
- queue saturation behavior;
- one user's failure does not stop later jobs;
- cancellation and graceful shutdown;
- active-user threshold boundary and duplicate profiles;
- repeated startup does not create an uncontrolled job storm.

---

## BACKEND-TEST-032 — PostgreSQL provider-specific integration matrix

Run mode: CI test lane  
Risk: P0/P1 false confidence from InMemory/SQLite

### Required work

1. Add a test factory/fixture using the workflow PostgreSQL service and isolated database/schema.
2. Run provider-sensitive focused suites against PostgreSQL, not only schema scripts.
3. Keep fast InMemory/SQLite tests, but label provider authority clearly.

### Required tests

- refresh-token generated value persistence and rotation race;
- serializable retry and named unique-constraint retry;
- XP `FOR UPDATE` concurrency;
- idempotency ledger concurrent insert/replay;
- offline answer unique constraints;
- outbox claim/lease race;
- migration-from-zero, pending-model-change and startup guard.

### Validation

The workflow must publish TRX and coverage artifacts for the PostgreSQL lane and fail on provider-specific regressions.

---

## BACKEND-TEST-033 — P0 mutation cancellation/rollback matrix

Run mode: endpoint/service integration tests  
Risk: P1 partial settlement during client disconnect/shutdown

### Scope

- quiz answer;
- SRS update;
- offline batch;
- Daily Run chest;
- economy spend/hint/reward;
- season claims;
- cosmetics claim/grant;
- shop purchase.

### Required tests per mutation

- cancellation before ledger/domain write;
- cancellation after pending ledger creation;
- cancellation after domain mutation but before completion/commit;
- retry after cancellation settles exactly once;
- no completed ledger with rolled-back domain state;
- no partial reward/balance/inventory mutation.

Use deterministic interceptors/barriers, not sleeps.

---

## BACKEND-TEST-034 — Legacy route parity and retirement

Run mode: contract tests + deprecation decisions  
Risk: P1/P2 duplicate settlement paths and mobile drift

### Required work/tests

- inventory every legacy coin/hint/avatar/quiz/batch alias;
- prove aliases delegate to canonical authority or are read-only;
- prove replay across alias and canonical route cannot double mutate;
- mark deprecated routes with explicit response/header/docs where appropriate;
- ensure mobile no longer calls routes selected for retirement;
- add route-presence tests to prevent accidental reintroduction.

Cross-repo mobile sync required.

---

## BACKEND-TEST-035 — Authorization-test anonymous accuracy audit

Run mode: test infrastructure audit  
Risk: P1 false security confidence

### Problem

Before this audit, `TestAuthHandler` authenticated requests without headers as `test-user`. Tests that omitted auth headers could be mislabeled as anonymous.

### Required work

1. Search all endpoint tests that expect 401/403 or use names such as Anonymous/Unauthenticated.
2. Use `X-Test-Anonymous: true` for true anonymous requests.
3. Keep separate tests for authenticated non-admin 403 behavior.
4. Add one direct `TestAuthHandler` test proving default user, explicit anonymous and role claims.
5. Prevent future security tests from conflating 401 and 403 unless both are genuinely acceptable.

### Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Authorization|Anonymous|AuthHandler|Monitoring|Maintenance|BugEndpoint"
```

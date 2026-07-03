# Backend Performance / Bug Follow-up Queue — 2026-07-03

Source audit: `../BACKEND_PERFORMANCE_BUG_AUDIT_2026_07_03.md`  
Previous queue: `backend_performance_optimization.md`

> These prompts are implementation packages. The source audit is static evidence only. Do not claim latency, throughput, memory or correctness improvement until the required tests and measurements run.

## Shared rules

- Preserve authenticated user scope and mobile response contracts.
- A mutation may be retried only when it has a proven idempotency/replay contract.
- Prefer PostgreSQL tests for locks, unique constraints, isolation and query plans.
- Every performance claim must include before/after query count, elapsed distribution, allocation/memory evidence or execution plan.
- Every bug fix must include the smallest regression test that fails before the fix.
- Do not move work from a request into an unbounded fire-and-forget task.
- Record cross-repo sync when request/response or retry semantics change.

## Priority table

| ID | Priority | Status | Purpose |
|---|---:|---|---|
| BE-PERF-009 | P1 | Prompt-ready | Bound and deduplicate weakness-analysis scheduling and stop full-history/full-taxonomy work per user. |
| BE-PERF-010 | P1 | Prompt-ready | Replace hourly all-profile XP reset work with set-based, cancellable and single-owner processing. |
| BE-PERF-011 | P1 | Prompt-ready | Bound rate-limit memory/cardinality and define multi-replica semantics. |
| BE-PERF-012 | P0 | Prompt-ready | Make adaptive answer submission atomic, idempotent and cancellation-correct before generic retry. |
| BE-PERF-013 | P1 | Prompt-ready | Remove refresh, snapshot, streak and reward mutations from GET/read paths. |
| BE-PERF-014 | P1 | Prompt-ready | Prevent explanation-cache stampedes, write-on-read amplification and expired-row growth. |
| BE-PERF-015 | P0/P1 | Prompt-ready | Make practice answer/completion concurrency settle exactly once and replay deterministically. |
| BE-PERF-016 | P0/P1 | Prompt-ready | Add outbox claim/lease/backoff/dead-letter and bounded idle polling. |
| BE-PERF-017 | P2/P1 | Prompt-ready | Measure and control tracing/logging overhead, cardinality and synchronous DB logging risk. |

---

## BE-PERF-009 — Bounded weakness-analysis pipeline

Priority: P1  
Related: BACKEND-TEST-031

### Inspect

- `src/MathLearning.Api/Services/WeaknessAnalysisScheduler.cs`
- `src/MathLearning.Api/Services/WeaknessAnalysisService.cs`
- `src/MathLearning.Api/Services/QuizAttemptIngestService.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- weakness scheduler/service tests

### Confirmed code risks

- unbounded `Channel<Guid>`;
- one consumer and no per-user deduplication;
- all active users loaded into memory on startup and every 24 hours;
- all attempts for one user loaded to calculate percentiles;
- complete Topics and Subtopics tables loaded for every analyzed user;
- no durable pending/running/completed state or queue-depth policy.

### Required work

1. Replace the unbounded queue with a configurable bounded queue or a durable job table.
2. Coalesce duplicate pending work by user id.
3. Define full-queue behavior explicitly: reject with metric, replace older duplicate or persist overflow; never silently allocate without bound.
4. Page/stream active users instead of one `ToListAsync` over the whole population.
5. Avoid scanning complete `QuizAttempts` history for every run. Use one of:
   - maintained rolling aggregates/quantile approximation;
   - a documented bounded lookback/window;
   - database-side aggregate computation with measured plan.
6. Load taxonomy names once per run or from a bounded versioned cache, not once per user.
7. Add per-user timeout/cancellation and ensure one slow user does not block the queue indefinitely.
8. Define restart semantics and multi-replica ownership.
9. Expose safe metrics: queue depth, deduplicated jobs, rejected jobs, age of oldest job, processing duration and failure count.

### Required tests

- 10,000 duplicate enqueues for one user create at most one pending unit of work;
- configured queue capacity is never exceeded;
- full-queue behavior matches the documented policy;
- cancellation stops producer and consumer without losing already persisted work;
- one failed/slow user does not block later users permanently;
- active-user scan uses pages with bounded materialization;
- taxonomy lookup count is constant per run, not per user;
- analysis query count and allocations do not grow linearly with total historic attempts when the new aggregate/window is used;
- restart resumes durable work or explicitly documents acceptable loss for best-effort mode;
- two replicas do not process the same durable user job concurrently.

### Measurement budget

Use fixtures of at least:

- 10,000 active users;
- one user with 100,000 attempts;
- 1,000 duplicate enqueue events.

Record peak managed allocation, queue depth, total SQL commands, p50/p95 per-user processing time and total daily-run duration.

### Completion rule

Do not mark complete from replacing `Channel.CreateUnbounded` alone. Completion requires bounded-memory evidence, duplicate coalescing, restart/multi-replica decision and a measured large-history test.

---

## BE-PERF-010 — Set-based and single-owner XP reset

Priority: P1

### Inspect

- `src/MathLearning.Api/Services/XpResetBackgroundService.cs`
- `src/MathLearning.Infrastructure/Services/XpTrackingService.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- schema/startup-state services
- XP reset/concurrency tests

### Confirmed code risks

- four `information_schema` probes every hour;
- all stale profiles loaded and tracked at day boundary;
- fallback branch sends one update per user;
- worker runs on every API replica;
- no `TimeProvider`, lease or deterministic schedule;
- database operations do not consistently receive the host cancellation token.

### Required work

1. Use startup `DatabaseSchemaState` or one cached schema capability snapshot; remove hourly column probes.
2. Decide between:
   - lazy period reset during authoritative XP reads/writes; or
   - scheduled set-based `ExecuteUpdate`/SQL statements.
3. If scheduled, use one distributed owner through PostgreSQL advisory lock/lease.
4. Execute daily, weekly and monthly resets as bounded set-based statements, not tracked entity materialization.
5. Make schedule deterministic with `TimeProvider` and explicit UTC boundary behavior.
6. Pass cancellation tokens through every query/update/delay.
7. Record rows affected and duration without logging user identifiers.
8. Define failure/retry behavior so a partial daily/weekly/monthly reset cannot create inconsistent period state.

### Required tests

- exact day, Monday/week and month/year boundary behavior;
- 100,000-profile fixture executes a fixed small number of SQL statements and does not track all profiles;
- two worker instances race and only one owns a period reset;
- lease loss/cancellation stops safely;
- restart after completed reset does not repeat destructive work;
- partial failure between period statements has a documented transaction/recovery outcome;
- missing-schema state skips safely without per-user fallback updates;
- XP award concurrent with reset preserves the documented authoritative total/period values;
- cancellation reaches database commands.

### Measurement budget

Record before/after:

- SQL statement count;
- rows materialized/tracked;
- peak allocations;
- lock duration;
- elapsed time for 1k, 10k and 100k profiles.

### Completion rule

No completion with a faster `foreach`. The reset must be set-based or deliberately eliminated through lazy reset, single-owner across replicas and covered at calendar boundaries.

---

## BE-PERF-011 — Bounded and replica-aware rate limiting

Priority: P1 security/performance

### Inspect

- `src/MathLearning.Api/Middleware/InMemoryRateLimitCounterStore.cs`
- `src/MathLearning.Api/Middleware/InMemorySlidingWindowRateLimitMiddleware.cs`
- `src/MathLearning.Api/Middleware/RateLimitClientIdentity.cs`
- forwarded-header middleware/configuration
- rate-limit tests and deployment replica settings

### Confirmed code risks

- dictionary keys are never removed;
- rejected requests are enqueued before the decision;
- per-key queues grow under sustained rejection during the window;
- cleanup happens only when the same key returns;
- `q.Count` is used under concurrency without a defined exactness contract;
- each replica enforces a separate limit;
- `Retry-After` always reports the complete configured window.

### Required work

1. Prefer ASP.NET Core rate-limiter primitives or implement a bounded, eviction-aware partition store.
2. Do not add rejected requests to unbounded per-key history unless the algorithm explicitly requires and bounds them.
3. Expire/remove idle partitions automatically.
4. Validate limit/window/key-cardinality configuration and reject pathological values at startup.
5. Return `Retry-After` based on the actual oldest accepted permit or documented algorithm.
6. Define production topology:
   - one-replica local limiter; or
   - distributed Redis/database limiter with atomic operation.
7. Keep trusted-proxy/IP resolution consistent with proxy middleware and prevent spoofed forwarded headers from creating arbitrary keys.
8. Add safe metrics for current partitions, evictions, allowed, rejected and store saturation.

### Required tests

- 100,000 one-shot unique keys return store cardinality near baseline after expiry/cleanup;
- sustained rejected traffic for one key remains bounded in memory;
- exact concurrent boundary: limit N allows exactly documented permits;
- window rollover and actual `Retry-After`;
- invalid zero/negative/extreme configuration fails startup safely;
- IPv4/IPv6/proxy identity normalization;
- untrusted `X-Forwarded-For` cannot create arbitrary partitions;
- two-replica test proves either shared limit or explicitly expected multiplied local limit;
- cancellation/request abort does not leak state.

### Measurement budget

Record allocation and lookup latency at 1k, 100k and 1m requests with high unique-key cardinality and sustained rejection.

### Completion rule

A timer that only trims queues is insufficient. Idle keys must be evicted, rejected traffic bounded and deployment-wide semantics documented and tested.

---

## BE-PERF-012 — Atomic/idempotent adaptive answer mutation

Priority: P0 correctness before performance

### Inspect

- `src/MathLearning.Api/Endpoints/AdaptiveEndpoints.cs`
- `src/MathLearning.Api/Services/AdaptiveApiFacade.cs`
- `src/MathLearning.Api/Services/AdaptiveLearningService.cs`
- `src/MathLearning.Api/Services/RetryPolicy.cs`
- adaptive entities/configuration/migrations
- anti-cheat, review schedule and legacy SRS dependencies
- adaptive integration/contract tests

### Confirmed code risks

- generic retry wraps a mutation without a settled replay contract;
- facade delegates omit the supplied cancellation token;
- no early replay branch for an answered `AdaptiveSessionItem`;
- no unique history constraint by adaptive session item;
- two `SaveChangesAsync` calls split authoritative state;
- legacy SRS runs after those saves;
- concurrent duplicate requests can both observe an unanswered item.

### Required work

1. Define stable operation identity. Prefer server-owned `AdaptiveSessionItemId` plus authenticated user and payload hash, or an explicit mobile operation id if cross-repo contract requires it.
2. Store a deterministic settled response or enough state to reconstruct exact replay.
3. Add a database uniqueness/atomic transition protecting one answer settlement per item.
4. Put session item, history, learning profile, review schedule, mastery and any authoritative anti-cheat record in one transaction where feasible.
5. Move non-authoritative downstream work to an idempotent durable outbox when it cannot share the transaction.
6. Remove generic mutation retry until the operation is replay-safe. Afterward retry only provider-transient failures with the same operation identity.
7. Pass the supplied cancellation token through facade, service and downstream calls.
8. Return conflict for same operation/item with materially different answer payload.
9. Preserve current mobile response shape or version/sync both repositories.

### Required tests

- first answer mutates exactly once and returns expected response;
- exact duplicate returns the identical settled response and adds no rows/counters/events;
- same item with different answer/confidence/time returns stable conflict;
- 20–50 concurrent identical submissions settle once;
- failure after SQL is issued but before commit leaves no partial history/review/mastery/item state;
- failure after authoritative commit but before downstream delivery is recoverable through durable work;
- process restart replay is deterministic;
- cancellation before commit rolls back; cancellation after settled commit returns/reconstructs settled result;
- PostgreSQL unique/locking race test;
- user A cannot submit user B's session item;
- legacy mobile alias/payload remains compatible.

### Performance budget

After correctness, record SQL command count and transaction duration for first settlement versus replay. Replay should avoid loading the full question/session graph where possible.

### Completion rule

Do not mark complete by adding another retry catch. Completion requires a database-enforced exactly-once settlement boundary, deterministic replay/conflict behavior and PostgreSQL concurrency evidence.

---

## BE-PERF-013 — Pure read paths and single school-leaderboard aggregation owner

Priority: P1

### Inspect

- `src/MathLearning.Infrastructure/Services/LeaderboardService.cs`
- `src/MathLearning.Infrastructure/Services/Leaderboard/SchoolLeaderboardAggregationService.cs`
- `src/MathLearning.Api/Endpoints/LeaderboardEndpoints.cs`
- `src/MathLearning.Api/Endpoints/ProgressEndpoints.cs`
- cosmetic reward settlement services
- schema-state/startup services
- leaderboard/progress endpoint tests

### Confirmed code risks

- school leaderboard GET can perform full aggregate refresh and write all aggregates;
- leaderboard GET can process cosmetic reward settlement with `CancellationToken.None`;
- leaderboard history GET can create snapshots;
- schema columns are probed through `information_schema` on reads;
- progress overview GET can roll streak, save profile and process rewards;
- a second incremental school aggregation service is registered but apparently unwired.

### Required work

1. Define GET routes as side-effect free: zero `SaveChanges`, reward grants, snapshots or full refreshes.
2. Move school aggregate refresh and snapshot capture to one background/durable owner with distributed lease.
3. Choose one aggregation model: scheduled full refresh or transaction/outbox-driven incremental aggregation. Remove or clearly retire the other.
4. Serve last-known aggregate with `IsStale`/generated timestamp when refresh is pending; do not refresh synchronously under user traffic.
5. Replace per-request schema probes with startup `DatabaseSchemaState` capability flags.
6. Move streak roll/reward settlement to an explicit mutation, login/activity write, or durable scheduled process with idempotency.
7. Ensure cosmetic reward settlement has stable source identity and is never triggered merely by polling a GET.
8. Forward request cancellation to all read queries.

### Required tests

- all affected GET routes execute zero database writes;
- repeated and concurrent GETs do not grant rewards or create snapshots;
- 100 concurrent stale leaderboard reads trigger at most one background refresh claim;
- stale data is served with explicit metadata while refresh is running/failing;
- schema probe count is zero after startup capability state is established;
- progress overview response remains compatible after streak settlement is decoupled;
- background refresh/snapshot/reward restart and duplicate-delivery behavior is idempotent;
- one aggregation implementation owns writes; registration/inventory test prevents a second active owner;
- cancellation stops read queries without starting detached work.

### Query budget

After schema state is cached:

- school leaderboard page including `MySchool`: target ≤3 SQL commands;
- school history read: target 1 SQL command and zero writes;
- progress overview: target ≤2 SQL commands and zero writes.

Record PostgreSQL plans for aggregate page/history indexes.

### Completion rule

No completion if GET merely dispatches unbounded `Task.Run`. Mutation work must have a durable, single-owner path and GET contract tests must prove zero writes.

---

## BE-PERF-014 — Explanation-cache single-flight and retention

Priority: P1

### Inspect

- `src/MathLearning.Api/Services/ExplanationCacheService.cs`
- `src/MathLearning.Api/Services/StepExplanationService.cs`
- explanation cache entity/configuration/migrations
- AI tutor enhancer/provider
- explanation endpoint/cache tests

### Confirmed code risks

- cold DB cache hit extends expiry and performs `SaveChangesAsync`;
- expired rows are not removed;
- concurrent identical misses all execute graph/mistake/formula/AI generation;
- concurrent inserts can race on the unique cache key;
- memory cache is populated before durable DB cache persistence completes;
- force refresh can amplify expensive generation.

### Required work

1. Make ordinary cache reads read-only (`AsNoTracking`) and choose explicit absolute or sliding expiry semantics.
2. If access touching is needed, batch/sample it asynchronously with bounded queue and no request dependency.
3. Add per-key single-flight in one process and a distributed lease/idempotent upsert for multiple replicas where AI cost warrants it.
4. Use atomic PostgreSQL upsert for cache writes.
5. Define ordering of memory/Redis/DB writes and behavior when one layer fails; avoid advertising a value as durably cached when persistence failed unless documented.
6. Add expired-row cleanup with bounded batch, index and retention metrics.
7. Rate-limit or operation-key `ForceRefresh` and prevent it from bypassing cost policy.
8. Bound serialized payload size and reject/cap pathological responses.
9. Emit hit/miss/stampede-suppressed/generation-duration/write-failure metrics without problem text.

### Required tests

- 50 concurrent identical misses invoke expensive generation once per instance and at most once cluster-wide under the chosen lease policy;
- all callers receive equivalent response;
- cold DB hit performs one read and zero writes;
- concurrent set uses one final row with no leaked unique exception;
- Redis unavailable falls back within a bounded timeout;
- DB persistence failure leaves documented memory/Redis state;
- expired row is treated as miss and cleanup removes it in bounded batches;
- force refresh obeys rate/cost limit and concurrent coalescing policy;
- payload-size boundary;
- cancellation of one waiter does not cancel shared generation needed by other waiters, while an abandoned sole generation is cancellable.

### Measurement budget

Record generation count, SQL/Redis operations, p50/p95 latency and allocations for cold single request, 50-request burst, warm memory hit and warm DB hit.

### Completion rule

A memory lock alone is not enough for multiple replicas. Completion needs atomic upsert, explicit expiry/cleanup and measured stampede suppression.

---

## BE-PERF-015 — Exactly-once practice answer and completion

Priority: P0/P1 correctness

### Inspect

- `src/MathLearning.Api/Services/PracticeSessionService.cs`
- practice endpoints/DTOs/entities/configuration
- `IPracticeAnalyticsUpdater`
- anti-cheat and background-job implementations
- practice integration/concurrency tests

### Confirmed code risks

- session/item and answered-state check occur before transaction/lock;
- no database-enforced one-settlement transition on the item;
- concurrent requests can both process analytics, anti-cheat and session counters;
- completion can be observed as Active by multiple callers and enqueue post-session jobs more than once;
- replay response is reconstructed only after seeing `AnsweredAt`, without an explicit payload-conflict rule.

### Required work

1. Use PostgreSQL `FOR UPDATE`, row version or atomic `UPDATE ... WHERE AnsweredAt IS NULL` to own the answer transition.
2. Define operation identity/payload hash and deterministic settled replay response.
3. Return conflict for a different answer payload against an already settled item.
4. Keep item/session counters, mastery and authoritative analytics/anti-cheat writes in one transaction or a same-transaction durable outbox.
5. Ensure next-question creation cannot duplicate under concurrent settlement.
6. Make completion an atomic state transition and enqueue post-session work through an idempotent outbox keyed by session.
7. Preserve current response contracts and ownership checks.
8. Propagate cancellation to every query/job enqueue.

### Required tests

- first answer settlement;
- exact duplicate returns same response with zero additional mutation;
- different-payload replay returns conflict;
- 20 concurrent submissions settle one item once and create at most one next question;
- session answered/correct/XP counters increment once;
- analytics, anti-cheat and mastery update once;
- failure after SQL before commit rolls back everything;
- concurrent complete calls transition once and enqueue exactly one post-session work item;
- restart replay remains deterministic;
- PostgreSQL lock/unique race and cancellation tests;
- cross-user access remains rejected.

### Performance budget

Measure first settlement versus replay SQL count and lock duration. Avoid loading every session item when only current/answered state and recent difficulty sequence are required; prove any query reduction preserves behavior.

### Completion rule

No completion from an application-level `if (AnsweredAt != null)` alone. The exactly-once decision must be database-enforced and downstream work idempotent.

---

## BE-PERF-016 — Claimed, backoff-aware outbox processing

Priority: P0/P1  
Related: BACKEND-TEST-023

### Inspect

- `src/MathLearning.Infrastructure/Services/EventBus/OutboxProcessor.cs`
- `OutboxMessage` entity/configuration/migrations
- event bus publishers/handlers
- background-worker health/metrics
- outbox tests

### Confirmed code risks

- every replica selects the same oldest unprocessed rows;
- no claim owner, lease expiration or `SKIP LOCKED`;
- publish happens before processed state save;
- permanent failures remain in the oldest batch;
- no `NextAttemptAt`, exponential backoff or terminal dead-letter cutoff;
- idle database polling occurs every second forever.

### Required work

1. Add atomic claim columns/state: claimed by, claimed at/lease until and status.
2. Claim batches in PostgreSQL with `FOR UPDATE SKIP LOCKED` or equivalent transaction-safe pattern.
3. Define at-least-once delivery explicitly and require idempotent consumers/event identity.
4. Add `NextAttemptAtUtc`, capped exponential backoff with jitter and configurable max attempts.
5. Move exhausted messages to terminal dead-letter state with safe admin redrive.
6. Prevent poison rows from blocking newer eligible messages.
7. Use adaptive idle delay or notification/wakeup; keep a bounded maximum poll interval for recovery.
8. Add lease recovery after worker crash and safe shutdown/cancellation.
9. Expose backlog, oldest age, claimed, retried, dead-letter and publish-duration metrics.
10. Bound/truncate `LastError`; never persist secrets or unbounded exception text.

### Required tests

- two processors claim disjoint messages;
- no simultaneous duplicate publish for one active lease;
- crash after claim allows recovery after lease expiry;
- crash after publish before processed save demonstrates documented at-least-once duplicate and consumer idempotency;
- poison message backs off and does not block later rows;
- max attempts moves to dead letter exactly once;
- redrive resets eligible state safely;
- idle query frequency follows configured adaptive budget;
- cancellation releases/stops claims without corrupt state;
- PostgreSQL index/query-plan test for eligible-message scan.

### Performance budget

For 100k backlog, record claim query plan, rows locked, batch throughput and database round trips. For idle state, record polls/minute per replica.

### Completion rule

Do not mark complete with a process-local semaphore. Multi-instance PostgreSQL claim evidence and poison/backoff behavior are mandatory.

---

## BE-PERF-017 — Measured observability with bounded cardinality

Priority: P2/P1

### Inspect

- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- `src/MathLearning.Api/Middleware/RequestPerformanceLoggingMiddleware.cs`
- `src/MathLearning.Infrastructure/Services/Performance/PerformanceDbCommandInterceptor.cs`
- `src/MathLearning.Api/Logging/PostgreSqlSink.cs`
- OpenTelemetry/Serilog deployment configuration

### Confirmed code risks

- custom request timing and Serilog request logging both execute;
- EF command interceptor is attached to both contexts;
- EF OTel instrumentation defaults on outside Development;
- no explicit sampler was visible;
- production minimum Warning can suppress Information performance records;
- raw path/user agent/IP/forwarded-for enrichment lacks explicit cardinality/privacy policy;
- a synchronous per-log-event PostgreSQL sink exists and could become a severe bottleneck if re-enabled.

### Required work

1. Define which layer owns request duration, status, route template and DB query count; remove duplicate low-value logging.
2. Use normalized route templates, not raw identifier-bearing paths, for metrics dimensions.
3. Configure trace sampling per environment and retain all error/slow traces through documented policy.
4. Emit performance events only for slow requests, query-budget violations or controlled sampling, while preserving aggregate metrics.
5. Ensure production receives usable budget evidence despite log-level configuration.
6. Remove the synchronous PostgreSQL sink or replace it with a bounded asynchronous batch sink with drop/backpressure policy; do not block request threads on log persistence.
7. Exclude tokens, raw answers, emails, user agents/IPs unless specifically required and governed.
8. Add exporter timeout/failure behavior that cannot stall requests or startup.
9. Document per-endpoint budgets and dashboards/queries that consume the telemetry.

### Required tests and benchmarks

- benchmark representative request with tracing/interceptor/logging on and off;
- route IDs produce one normalized metric series, not one series per ID;
- slow/error/query-budget violations are retained under sampling;
- normal production request creates the documented number of log events;
- disabled exporter does not block requests;
- logging sink saturation follows bounded drop/backpressure policy;
- no synchronous `SaveChanges` occurs from request logging;
- sensitive/high-cardinality values are absent from exported attributes.

### Measurement budget

Record p50/p95 latency, allocations and CPU for no observability, target production observability and full debug instrumentation. State acceptable overhead before implementation; do not invent a success percentage afterward.

### Completion rule

No completion from adding more logs. The result must reduce ambiguity, have measured overhead, normalized dimensions and a safe production sampling/export policy.

## Recommended execution order

1. BE-PERF-012
2. BE-PERF-015
3. BE-PERF-011
4. BE-PERF-013
5. BE-PERF-009
6. BE-PERF-010
7. BE-PERF-016 together with BACKEND-TEST-023
8. BE-PERF-014
9. BE-PERF-017

## Common validation after each package

```text
dotnet build MathLearning.slnx -c Release
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "<focused package filter>"
```

Use PostgreSQL 16 CI/test infrastructure for every prompt involving locks, claims, isolation, upserts or query plans. Record GitHub Actions evidence before marking Validated.

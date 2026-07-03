# Backend Performance and Bug Audit — 2026-07-03

> Static code audit only. This document identifies code-level risks and creates implementation prompts. It is not proof that a problem occurred in production and it is not proof that a fix has landed.

Repo: `ivanjovicic/MathLearning`  
Run log: `.ai/runs/2026-07-03-BACKEND-PERF-AUDIT-004-evidence.md`  
Detailed queue: `prompt_queues/backend_performance_followups_2026_07_03.md`

## Scope

The audit reviewed the current performance queue and then inspected active runtime code around:

- adaptive answer mutations and retry behavior;
- weakness analysis scheduling and per-user recomputation;
- XP reset background work;
- rate limiting state;
- school leaderboard refresh/reward paths;
- progress read paths;
- explanation cache behavior;
- practice-session answer/completion concurrency;
- outbox polling and failure handling;
- request/EF observability overhead.

Existing prompts BE-PERF-001…008 were not duplicated.

## Executive priority

| Priority | Finding | Main risk | Prompt |
|---|---|---|---|
| P0 | Adaptive answer retry is not idempotent or atomic | duplicate history/anti-cheat/SRS/mastery effects and partial commits | BE-PERF-012 |
| P0/P1 | Practice answer/completion concurrency lacks a settled replay contract | double aggregate/background effects and race-dependent response | BE-PERF-015 |
| P1 | GET/read paths execute writes and expensive refreshes | thundering herd, write amplification, rewards triggered by reads | BE-PERF-013 |
| P1 | Weakness scheduler/analysis is unbounded and recomputes from all attempts | memory/DB growth with active users and history | BE-PERF-009 |
| P1 | XP reset scans/tracks all eligible profiles and can run on every replica | midnight memory spike, update storm, duplicate work | BE-PERF-010 |
| P1 | In-memory rate limiter has unbounded key/queue growth | memory exhaustion under abuse and inconsistent multi-replica limits | BE-PERF-011 |
| P1 | Explanation cache writes on DB reads and has no stampede/cleanup contract | hot-row writes, duplicate generation, unique conflicts, table growth | BE-PERF-014 |
| P1 | Outbox has no claim/lease/backoff/dead-letter cutoff | duplicate publish, poison batch churn, constant polling | BE-PERF-016 |
| P2/P1 | Observability is duplicated and not tied to controlled sampling/budgets | overhead or missing production evidence, high-cardinality telemetry | BE-PERF-017 |

---

## Finding 1 — Adaptive answer mutation can be retried after partial success

### Code evidence

`AdaptiveApiFacade.SubmitAdaptiveSessionAnswerAsync` wraps the mutation in generic `RetryPolicy.ExecuteAsync`, but its delegate calls the service overload without the request cancellation token. The same pattern exists on other adaptive facade calls.

`AdaptiveLearningService.SubmitAnswerAsync`:

1. loads and mutates the adaptive session item;
2. adds `UserQuestionHistory`;
3. calls anti-cheat and review-schedule logic;
4. executes the first `SaveChangesAsync`;
5. queries pending items;
6. updates session completion and topic mastery;
7. executes a second `SaveChangesAsync`;
8. calls legacy SRS afterward.

There is no early replay branch for an already answered `AdaptiveSessionItem`, no unique index tying `UserQuestionHistory` to `AdaptiveSessionItemId`, and no single transaction covering all authoritative writes.

### Failure scenario

A transient `DbUpdateException`, timeout or process failure after the first save can leave history/review/item state committed but mastery/session completion unfinished. Generic retry then repeats a mutation that does not yet have a stored-result replay contract.

Concurrent duplicate requests can both observe an unanswered item before either saves.

### Required action

BE-PERF-012 must be treated as correctness work before adaptive throughput tuning.

---

## Finding 2 — Weakness analysis cost grows with total history and active-user cardinality

### Code evidence

`WeaknessAnalysisScheduler` uses `Channel.CreateUnbounded<Guid>()`, has one consumer, no deduplication, no persisted delivery and no queue-depth limit.

The daily hosted service:

- runs immediately on startup;
- loads every profile active in the last 30 days into a list;
- enqueues every mapped user;
- repeats every 24 hours from process start rather than at a controlled calendar time.

For each user, `WeaknessAnalysisService.AnalyzeUserAsync` loads:

- all topic stats;
- all subtopic stats;
- all existing weakness rows;
- all `QuizAttempts` for the user;
- the complete `Topics` dictionary;
- the complete `Subtopics` table.

Percentiles and grouping are calculated in process memory. The full taxonomy is reloaded for every user.

### Risk

Backlog and memory can grow without a bound. One slow user blocks the only consumer. Total work is approximately active users multiplied by each user's complete attempt history, plus repeated taxonomy reads.

### Required action

BE-PERF-009 defines bounded queueing, dedupe, paging, rolling aggregates and load budgets. It complements BACKEND-TEST-031.

---

## Finding 3 — XP reset performs repeated schema probes and mass tracked updates

### Code evidence

`XpResetBackgroundService` runs every hour. Each iteration checks four columns through separate `information_schema.columns` queries.

When the expected columns exist, it loads every profile whose reset date is stale into EF tracking, mutates every entity and calls one `SaveChangesAsync`. Around a day boundary this can mean the entire user table.

When some columns are missing, it loads a list of all eligible users and sends one raw `UPDATE` per user.

The worker is registered on every API replica and has no distributed lease. Most queries and writes do not receive the host cancellation token.

### Risk

- memory and change-tracker spike at reset time;
- a large update burst and long transaction;
- N updates in the fallback branch;
- duplicate work across replicas;
- hourly metadata-query overhead;
- slow shutdown or work continuing after cancellation.

### Required action

BE-PERF-010 requires startup-cached schema state, set-based updates or deliberate lazy reset, distributed ownership, boundary tests and PostgreSQL measurements.

---

## Finding 4 — In-memory rate-limit state is unbounded

### Code evidence

`InMemoryRateLimitCounterStore` stores:

```text
ConcurrentDictionary<string, ConcurrentQueue<long>>
```

Each request is enqueued before the limit decision. Rejected requests therefore continue increasing the queue during the window. Expired timestamps are removed only when the same key is used again, while dictionary keys are never removed.

The middleware uses one singleton store per process. Different replicas have independent limits.

### Risk

- a large number of unique IP/user keys creates permanent dictionary growth;
- sustained rejected traffic grows per-key queues;
- queue counting and cleanup cost increases with abuse volume;
- multi-replica effective limit is multiplied by replica count;
- `Retry-After` always reports the full window rather than the actual oldest-event expiry.

### Required action

BE-PERF-011 requires bounded partition state, eviction, exact concurrency tests and an explicit single-instance versus distributed policy.

---

## Finding 5 — School leaderboard and progress GET paths perform mutations

### School leaderboard

`GetSchoolLeaderboardAsync` calls `HasSchoolLeaderboardSchemaAsync`, which performs multiple `information_schema` queries on each request. It then calls `EnsureCurrentPeriodAsync`; when no aggregate row is fresh, the GET request performs a full school aggregate refresh and writes all rows.

The same GET can call `ICosmeticRewardService.ProcessRewardSourceAsync` for the current user's school placement using `CancellationToken.None`.

`GetSchoolLeaderboardHistoryAsync` calls `CaptureSnapshotAsync` and writes snapshot rows when history is empty.

### Progress overview

`GET /api/progress/overview` loads a tracked profile, applies `StreakRoller`, saves the profile and processes cosmetic rewards when a roll occurs.

### Additional architecture drift

`SchoolLeaderboardAggregationService` is registered but current code search found no caller beyond registration. It contains a second aggregate-update strategy that would recompute ranks for every school in each touched period if wired. The project therefore has two competing aggregation models: refresh-on-read and an apparently unused incremental service.

### Risk

- concurrent stale GET requests can all refresh aggregates;
- reads contend with writes and can become unexpectedly slow;
- retries/crawlers/polling can cause mutation work;
- reward settlement is coupled to a read and ignores request cancellation;
- schema metadata queries inflate every leaderboard request;
- future wiring of the second aggregation service can duplicate work.

### Required action

BE-PERF-013 requires pure read contracts, background refresh/snapshot/reward settlement, cached schema state and a single documented aggregation owner.

---

## Finding 6 — Explanation cache has write amplification and stampede risk

### Code evidence

On a database cache hit, `ExplanationCacheService.GetAsync` loads a tracked entity, extends expiry and calls `SaveChangesAsync`. A read therefore becomes a write whenever memory/Redis misses but DB hits.

Expired rows return a miss but are not removed. No cleanup worker or bounded retention policy is visible.

On a miss, `StepExplanationService` performs graph/mistake/formula/optional AI work and then calls cache `SetAsync`. There is no per-key single-flight or distributed lock. Concurrent identical misses can all generate and then race on the unique cache index.

### Risk

- DB write on read and hot-row contention;
- repeated expensive generation during a burst;
- unique-constraint failures during concurrent insert;
- unbounded expired-row accumulation;
- force-refresh amplification;
- inconsistent behavior when memory is populated before DB persistence fails.

### Required action

BE-PERF-014 defines single-flight, upsert, cleanup, absolute/sliding policy and query/write budgets.

---

## Finding 7 — Practice-session mutation has a concurrency window

### Code evidence

`PracticeSessionService.SubmitAnswerAsync` reads the session and all session items before beginning a relational transaction. It checks `AnsweredAt` before the transaction. `PracticeSessionItem` has no row-version or unique settlement record for the answer operation.

Two requests can therefore observe the same unanswered item and both enter the mutation path. The method updates mastery, analytics, anti-cheat and next-question state before saving.

`CompleteSessionAsync` is replay-safe for a request that sees `Completed`, but two concurrent requests can both see `Active` and enqueue post-session jobs after separate saves.

### Risk

Race-dependent counters, duplicate analytics/anti-cheat effects, duplicate post-session jobs and inconsistent replay responses.

### Required action

BE-PERF-015 requires a row lock or atomic state transition, stored replay result/operation identity and concurrency tests on PostgreSQL.

---

## Finding 8 — Outbox polling can churn on poison and duplicate work

### Code evidence

`OutboxProcessor` polls every second, selects the oldest 50 unprocessed rows and processes them without a claim/lease. Failed rows remain unprocessed with only `Attempts` and `LastError`; there is no visible next-attempt time, exponential backoff, terminal dead-letter transition or maximum attempt cutoff.

Multiple replicas can select and publish the same rows. A permanently failing group in the oldest batch is re-read every second indefinitely.

### Required action

BE-PERF-016 adds performance/backpressure requirements to the correctness work already tracked by BACKEND-TEST-023.

---

## Finding 9 — Observability has an unclear overhead/evidence contract

### Code evidence

The pipeline includes both custom request-performance middleware and Serilog request logging. EF command counting is always attached to both DbContexts. EF OpenTelemetry instrumentation defaults to enabled outside Development. No explicit trace sampler was visible in service registration.

Production Serilog minimum level is Warning, while request-performance logs are Information, so the custom timing/query-count records can be suppressed precisely where production evidence is needed.

Serilog enrichment includes raw request path, user agent, client IP and forwarded-for values; route-template normalization and cardinality policy are not explicit.

A synchronous `PostgreSqlSink` exists that creates a scope and performs `SaveChanges()` per emitted event, although current code search found no active registration. It is a dangerous latent implementation if re-enabled.

### Risk

The project can pay tracing/interceptor overhead without usable production budgets, or produce high-cardinality telemetry. Re-enabling the DB sink would put synchronous database writes on logging call sites.

### Required action

BE-PERF-017 requires benchmarked sampling, route normalization, slow-request/query-budget events and an explicit ban or asynchronous design for DB logging.

---

## Existing prompts that remain relevant

The following existing work should not be duplicated:

- BE-PERF-001 — quiz start;
- BE-PERF-002 — SRS daily/mixed;
- BE-PERF-003 — answer/offline transaction timeline;
- BE-PERF-004 — DB leaderboard rank fallback;
- BE-PERF-005 — Redis startup/fallback;
- BE-PERF-006 — cold-start/background budget;
- BE-PERF-007 — performance budgets/observability;
- BE-PERF-008 — route compatibility;
- BACKEND-TEST-022 — durable analytics ingest handoff;
- BACKEND-TEST-023 — multi-instance outbox correctness;
- BACKEND-TEST-031 — weakness scheduler durability/backpressure;
- BACKEND-TEST-032 — PostgreSQL provider lane;
- BACKEND-TEST-042 — distributed maintenance lock;
- BACKEND-TEST-045/046 — database paging and remaining pagination inventory;
- BACKEND-TEST-048 — index-bloat metric validity.

## Recommended execution order

1. BE-PERF-012 — adaptive mutation correctness.
2. BE-PERF-015 — practice mutation concurrency.
3. BE-PERF-011 — rate-limit memory/security.
4. BE-PERF-013 — remove writes from read paths.
5. BE-PERF-009 — weakness pipeline scaling.
6. BE-PERF-010 — XP reset scaling.
7. BE-PERF-016 + BACKEND-TEST-023 — outbox claim/backoff/dead-letter.
8. BE-PERF-014 — explanation cache stampede/retention.
9. BE-PERF-017 — measured observability policy.

## Validation status

No executable .NET checkout or runtime metrics were available in this connector session. Findings are based on current code paths. Each implementation prompt requires focused tests plus measured evidence before any performance improvement is claimed.

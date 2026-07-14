# BACKEND-PERF-AUDIT-004 Evidence

Prompt ID: BACKEND-PERF-AUDIT-004
Queue: `docs/prompt_queues/backend_performance_optimization.md`, follow-up queue `docs/prompt_queues/backend_performance_followups_2026_07_03.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: docs/audit + prompt creation
Started from queue status: ad-hoc continuation

## Goal

Analyze the current MathLearning backend for performance risks and likely bugs, distinguish confirmed code-level findings from hypotheses, and add precise implementation prompts to the performance queue.

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001
- BACKEND-MISTAKE-IDEM-001
- BACKEND-MISTAKE-PERF-001
- BACKEND-MISTAKE-QUEUE-001

## Guardrails used

- Audit only; no runtime-fix claim.
- Existing BE-PERF-001…008 and BACKEND-TEST prompts were read before allocating new IDs.
- New prompt IDs were searched before publication.
- Correctness risks were ranked ahead of micro-optimizations.
- Every prompt contains exact inspect paths, required tests, measurement evidence, provider requirements and a completion rule.
- No latency/memory/throughput percentage was invented.

## Current code reviewed

- `src/MathLearning.Api/Services/WeaknessAnalysisScheduler.cs`
- `src/MathLearning.Api/Services/WeaknessAnalysisService.cs`
- `src/MathLearning.Api/Services/XpResetBackgroundService.cs`
- `src/MathLearning.Api/Middleware/InMemoryRateLimitCounterStore.cs`
- `src/MathLearning.Api/Middleware/InMemorySlidingWindowRateLimitMiddleware.cs`
- `src/MathLearning.Api/Middleware/RateLimitClientIdentity.cs`
- `src/MathLearning.Api/Services/AdaptiveApiFacade.cs`
- `src/MathLearning.Api/Services/AdaptiveLearningService.cs`
- `src/MathLearning.Application/Services/IAdaptiveLearningService.cs`
- `src/MathLearning.Api/Services/RetryPolicy.cs`
- `src/MathLearning.Api/Services/PracticeSessionService.cs`
- `src/MathLearning.Infrastructure/Services/LeaderboardService.cs`
- `src/MathLearning.Infrastructure/Services/Leaderboard/SchoolLeaderboardAggregationService.cs`
- `src/MathLearning.Api/Endpoints/ProgressEndpoints.cs`
- `src/MathLearning.Api/Services/ExplanationCacheService.cs`
- `src/MathLearning.Api/Services/StepExplanationService.cs`
- `src/MathLearning.Infrastructure/Services/EventBus/OutboxProcessor.cs`
- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- `src/MathLearning.Api/Middleware/RequestPerformanceLoggingMiddleware.cs`
- `src/MathLearning.Infrastructure/Services/Performance/PerformanceDbCommandInterceptor.cs`
- `src/MathLearning.Api/Logging/PostgreSqlSink.cs`
- relevant EF model/index configuration and current queues/audits.

## Confirmed code-level findings

1. Weakness queue is unbounded, not deduplicated and recomputes from complete user attempt history while reloading full taxonomy per user.
2. XP reset performs hourly schema probes, all-profile tracked updates or per-user fallback updates, and runs on every replica without a lease.
3. Rate-limit dictionary keys are not removed and rejected requests are added to per-key queues; replica semantics are process-local.
4. Adaptive answer mutation is wrapped in generic retry despite two saves, no settled replay result, no unique item-settlement constraint and dropped request cancellation.
5. School leaderboard/progress GET paths can refresh/write aggregates, create snapshots, roll streak state and process rewards.
6. Explanation DB cache hits write expiry state, expired rows accumulate and concurrent misses have no single-flight/upsert contract.
7. Practice answer/completion checks occur before database-enforced transition ownership, leaving duplicate concurrency windows.
8. Outbox polls every second with no claim/lease, attempt backoff, terminal dead-letter cutoff or poison-message isolation.
9. Observability has duplicate request timing, unclear production sampling/evidence, raw/high-cardinality enrichment and a latent synchronous PostgreSQL log sink.

These are static code findings. Production frequency and measured impact remain unverified.

## Documents added

- `docs/BACKEND_PERFORMANCE_BUG_AUDIT_2026_07_03.md`
- `docs/prompt_queues/backend_performance_followups_2026_07_03.md`

## Queue prompts added

- BE-PERF-009 — bounded weakness-analysis pipeline;
- BE-PERF-010 — set-based/single-owner XP reset;
- BE-PERF-011 — bounded and replica-aware rate limiter;
- BE-PERF-012 — atomic/idempotent adaptive answer mutation;
- BE-PERF-013 — pure read paths and one school aggregation owner;
- BE-PERF-014 — explanation cache single-flight/retention;
- BE-PERF-015 — exactly-once practice answer/completion;
- BE-PERF-016 — claimed/backoff-aware outbox;
- BE-PERF-017 — measured, bounded-cardinality observability.

## Documentation reconciled

- `docs/DOCS_INDEX.md` links the current audit and follow-up queue.
- `docs/ai/learning/MISTAKE_LEDGER.md` adds:
  - BACKEND-MISTAKE-IDEM-002 — generic retry around a non-idempotent multi-save mutation;
  - BACKEND-MISTAKE-PERF-002 — read endpoints performing refresh/settlement/snapshot writes;
  - BACKEND-MISTAKE-PERF-003 — unbounded keyed in-memory state.

## Validation performed

Static verification only:

- searched existing prompt IDs to avoid BE-PERF collisions;
- confirmed existing BE-PERF-001…008 topics were not duplicated;
- checked current implementation and EF index/uniqueness configuration for each finding;
- confirmed adaptive service interface has no cancellation-token overload;
- confirmed no focused tests were found for rate-limit-store cardinality, XP reset worker or weakness scheduler behavior;
- confirmed `SchoolLeaderboardAggregationService` is registered but current code search found no active caller;
- confirmed synchronous `PostgreSqlSink` exists but current code search found no active registration.

## Validation not run

No executable .NET checkout, PostgreSQL fixture, load generator or completed GitHub Actions run was available. Therefore:

- no build/test pass is claimed;
- no production bug occurrence is claimed;
- no latency, allocation or query-count improvement is claimed;
- all new queue rows remain `Prompt-ready`.

Implementation packages must run at minimum:

```text
dotnet build MathLearning.slnx -c Release
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "<focused prompt filter>"
```

PostgreSQL 16 evidence is mandatory for BE-PERF-012, 013, 014, 015 and 016 where locking, upsert, claim or execution-plan behavior is involved.

## Recommended order

1. BE-PERF-012 — adaptive mutation correctness.
2. BE-PERF-015 — practice mutation concurrency.
3. BE-PERF-011 — rate-limit memory/security.
4. BE-PERF-013 — remove mutations from GET/read paths.
5. BE-PERF-009 — weakness scaling.
6. BE-PERF-010 — XP reset scaling.
7. BE-PERF-016 with BACKEND-TEST-023 — outbox correctness/performance.
8. BE-PERF-014 — explanation cache.
9. BE-PERF-017 — observability overhead.

## Residual uncertainty

- Actual production cardinalities, request rates, number of replicas and PostgreSQL plans were not available.
- Some paths may be feature-disabled or rarely called; implementation prompts must first capture baseline usage.
- Static search cannot prove that reflection/external scheduling does not invoke apparently unwired services.
- Mobile retry behavior must be checked before changing adaptive/practice response semantics.

## Completion

85%

Score is capped because this was a docs/static audit without executable validation.

Commit SHA: 2ee5ad8260334ca1984bc1c29c2f5bdf7bba486c

## Key commits

- `9f7965be858ec752c9432a892f9abf69be5087ae` — evidence started
- `4bf9b9c9c64d0769909006c7cd196d5f54dc6b5d` — performance/bug audit
- `fa73339b92e9203d55eeac1f16c8fad75b211d27` — detailed BE-PERF-009…017 queue
- `102b5fdf58b630e1cbd652deff24a6dee1803ead` — documentation index
- `940f739e795b5dfe31ca86dd57b2cdc527fc1d71` — mistake ledger learning

## Cross-repo sync

Deferred/not required for this audit because no runtime or mobile contract changed. BE-PERF-012 and BE-PERF-015 must explicitly inspect and update mobile retry/contract docs if their implementation changes request identity, conflict status or replay response semantics.

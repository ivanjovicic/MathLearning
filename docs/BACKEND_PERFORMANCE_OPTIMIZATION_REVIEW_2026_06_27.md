# Backend Performance / Optimization Review — 2026-06-27

Status: planning + one small runtime optimization landed  
Repo: `ivanjovicic/MathLearning`

This review focuses on backend performance, reliability, mobile comfort, offline/idempotency safety, and the highest-leverage next prompts.

Related docs:

- `docs/DOCS_INDEX.md`
- `docs/AGENT_QUICKSTART.md`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/backend_contract_gap_report.md`
- `docs/BACKEND_REVIEW_2026_06_27.md`
- `docs/prompt_queues/backend_performance_optimization.md`

---

## 1. Executive finding

The backend is already much more advanced than a basic API:

- global EF query splitting is configured for `ApiDbContext` and `AppDbContext`;
- a `PerformanceDbCommandInterceptor` is registered;
- OpenTelemetry + ASP.NET Core + HttpClient + optional EF instrumentation are wired;
- PostgreSQL health checks, schema guard, Redis/DB leaderboard fallback, Hangfire fallback, idempotency ledgers, sync endpoints, and many indexes exist;
- mobile contract/idempotency docs and tests already exist.

The next backend direction should be:

```text
Protect mobile hot paths first: quiz start, SRS daily, answer/offline replay, leaderboard fallback, progress/adaptive reads, and cold-start/background-job startup behavior.
```

---

## 2. Runtime improvement applied now

### DB-backed leaderboard period sorting + projection

Changed file:

- `src/MathLearning.Infrastructure/Services/Leaderboard/DbBackedRedisLeaderboardService.cs`

Problem found:

- `GetLeaderboardAsync` normalized the period, but still always ordered by `WeeklyXp`.
- It loaded full `UserProfile` entities for the top rows, even though the response only needs user id, display name, level, score, weekly XP, and streak.

Change:

- `GetLeaderboardAsync` now uses the existing `OrderByScore(...)` period-aware order.
- It projects only the fields required for `LeaderboardEntryDto` through `ProjectScores(...)` before materialization.
- `ProjectScores(...)` now keeps display-name fallback EF-friendly by projecting an empty string and applying `User{userId}` fallback after materialization.

Expected effect:

- DB fallback leaderboard now respects day/week/month/all-time period ordering.
- DB fallback reads less data for leaderboard rows.
- Redis outage or missing Redis config should be less likely to return misleading period results.

Validation still required:

```bash
dotnet format --verify-no-changes
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~Leaderboard"
```

---

## 3. Highest-impact backend findings

### 3.1 Quiz start / questions hot path

Code signal:

- `/api/quiz/start` creates a quiz session, selects random question ids, loads full question details, maps DTOs, then saves the session.
- legacy `/api/quiz/questions` builds a similar response and also creates a `QuizSession`.
- question loading correctly uses selected ids + `AsSplitQuery`, but selection still does a count + skip/take and can do a wrap-around query.

Risk:

```text
Quiz start is the mobile app's most important perceived-speed path. Extra DB round-trips here directly delay first question rendering.
```

Prompt: `BE-PERF-001`.

---

### 3.2 Daily SRS hot path

Code signal:

- `/api/quiz/srs/daily` loads due question ids from `QuestionStats`, then loads full question details.
- If due questions are fewer than target, it pads with random questions from all questions not in the due set.

Risk:

```text
The mobile app may call Daily SRS count/questions frequently. This endpoint needs a strict no-duplicate-fetch, index-aware, bounded-query shape.
```

Prompt: `BE-PERF-002`.

---

### 3.3 Answer submit / offline replay transaction path

Code signal:

- `/api/quiz/answer` supports idempotency and serializable retry.
- `ProcessAnswerAttemptWithinTransactionAsync` checks existing answer, updates stats, loads profile, awards XP, inserts answer and audit, and returns ingest data.
- offline batch uses a serializable transaction, loads existing answer keys, loads questions with options, loops answers, and then calculates overview.

Risk:

```text
This is the core correctness path. Optimization must not weaken idempotency, XP authority, duplicate answer protection, offline replay, or first-correct uniqueness.
```

Prompt: `BE-PERF-003`.

---

### 3.4 DB-backed leaderboard fallback still has rank inefficiency

Code signal:

- `GetUserRankAsync` orders all matching user ids into memory and then uses `FindIndex` to compute rank.
- `GetNearRivalsAsync` calls `GetUserRankAsync`, then runs another ordered query with skip/take.

Risk:

```text
When Redis is missing/down, rank lookup can become O(N) memory work and the leaderboard endpoint calls it when includeMe=true.
```

Prompt: `BE-PERF-004`.

---

### 3.5 Redis startup/cold-start behavior

Code signal:

- Redis is optional; without Redis the app falls back to DB-backed leaderboard.
- With a Redis connection string, registration uses `ConnectionMultiplexer.Connect(...)` during service factory execution.

Risk:

```text
Misconfigured or slow Redis can hurt cold start or runtime resolution. Redis should be configured with bounded connect/retry behavior and explicit fallback policy.
```

Prompt: `BE-PERF-005`.

---

### 3.6 Startup/background jobs

Code signal:

- Hangfire startup probes PostgreSQL before enabling background jobs.
- If unavailable, it registers a disabled background job client and logs the fallback.
- Hosted services include index maintenance, XP reset, weakness analysis scheduler, and daily hosted service.

Risk:

```text
Cold start can become heavy if migrations/schema checks, seeders, token initialization, background job setup, or hosted service startup work is not bounded and measured.
```

Prompt: `BE-PERF-006`.

---

### 3.7 Observability exists, but budgets are not yet explicit

Code signal:

- OpenTelemetry tracing is configured; EF tracing defaults to enabled outside development.
- `PerformanceDbCommandInterceptor` is registered.
- Global request logging is present.

Risk:

```text
Instrumentation exists, but prompts need budgets: quiz start, SRS daily, answer submit, offline batch, leaderboard, progress/adaptive reads, and startup.
```

Prompt: `BE-PERF-007`.

---

### 3.8 Endpoint bloat / route compatibility pressure

Code signal:

- There are canonical routes plus mobile compatibility aliases across quiz, leaderboard, cosmetics/avatar, and other endpoint families.

Risk:

```text
Keeping legacy routes alive is useful for mobile compatibility, but every route should have owner, status, test/smoke evidence, and deprecation plan.
```

Prompt: `BE-PERF-008`.

---

## 4. Priority stack

| Priority | Work | Why |
|---:|---|---|
| P0 | `BE-PERF-001` quiz start query budget | Directly affects first question latency. |
| P0 | `BE-PERF-002` Daily SRS query budget | Directly affects Daily Review and mobile redirect/cache flow. |
| P0 | `BE-PERF-003` answer/offline replay transaction audit | Core data integrity and perceived answer speed. |
| P1 | `BE-PERF-004` leaderboard rank fallback | Prevents DB fallback from becoming O(N). |
| P1 | `BE-PERF-005` Redis startup/fallback policy | Cold-start and reliability. |
| P1 | `BE-PERF-006` startup/background service budget | Cold-start and Fly/container behavior. |
| P1 | `BE-PERF-007` explicit performance budgets | Turns instrumentation into actionable guardrails. |
| P2 | `BE-PERF-008` route compatibility/deprecation audit | Keeps API surface manageable. |

---

## 5. What not to optimize casually

Do not do these without a focused prompt and tests:

- weakening idempotency ledger behavior;
- moving XP/reward authority to client;
- removing serializable retry or uniqueness constraints from mutation paths;
- replacing safe `AsSplitQuery` includes with single-query graph loads without measuring;
- caching child-specific or free-form explanation payloads without privacy policy;
- broad route cleanup that breaks mobile compatibility;
- background job changes without startup/health evidence.

---

## 6. Definition of done for future backend performance prompts

Every backend performance prompt must report:

```text
Changed:
Hot path addressed:
Expected effect:
Contract/idempotency risk:
Validation actually run:
Validation not run:
Tests added/updated:
Residual risk:
Next prompt:
```

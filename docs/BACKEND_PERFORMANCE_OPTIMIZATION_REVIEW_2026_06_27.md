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

Implementation status:

- `src/MathLearning.Api/Endpoints/SrsEndpoints.cs` now routes the daily and mixed due scans through a shared `BuildDueQuestionStatsQuery(...)` helper ordered by `NextReview`, `Ease`, and `QuestionId` so the existing `UserId + NextReview` index stays aligned with the hot path.
- `tests/MathLearning.Tests/Endpoints/SrsEndpointsIntegrationTests.cs` now covers due-only, due+random padding, no-due, limit, and mixed disjointness behavior.

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

Implementation status:

- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs` now keeps the `/api/quiz/answer` replay path ledger-only until it knows the request is fresh, then loads the question graph and language settings only for real processing.
- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs` also reads replay-only entities with `AsNoTracking()` in the offline batch session/question lookups and duplicate-answer checks.
- The transaction timeline is still intentionally serializable:
  1. `/api/quiz/answer` begins the idempotency-ledger / mutation transaction, checks for replay, and only then loads question data and user language for fresh processing before ensuring the quiz session, updating `UserQuestionStats`, updating `UserProfiles`, inserting `UserAnswers` and `UserAnswerAudits`, saving, committing, and ingesting after commit.
  2. `/api/quiz/offline-submit` and `/api/quiz/batch-submit` begin a serializable batch transaction, ensure the session, load existing answer keys, load the question graph, loop the accepted answers, save, commit, and then calculate overview / ingest after commit.
- DB calls that remain required inside the transaction:
  - idempotency begin/complete and replay state writes;
  - question graph and language reads for fresh quiz answers only;
  - `UserQuestionStats` load + update for first-correct uniqueness and attempt accounting;
  - `UserProfiles` read-modify-write for XP and streak-aware progress;
  - `UserAnswers` and `UserAnswerAudits` inserts;
  - `QuizSessions` insert when the batch/session is missing;
  - offline existing-answer-key lookup, because it protects the batch replay window from double import;
  - `CalculateUserOverview`, because it can still persist streak roll state.
- Candidate for later work:
  - moving incorrect-answer explanation lookup out of the transaction if we can still keep the stored replay body identical.
  - broader batch ingestion decomposition into smaller commits or a dedicated replay ledger, but only if we can keep duplicate replay, audit rows, and first-correct behavior exactly the same.
- Coverage already in place for this prompt:
  - `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs`
  - `tests/MathLearning.Tests/Endpoints/OfflineBatchSubmitCompatibilityTests.cs`
  - `tests/MathLearning.Tests/Contracts/MobileMutationContractIntegrationTests.cs`
  - `tests/MathLearning.Tests/Services/XpTrackingConcurrencyIntegrationTests.cs`

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

Implementation status:

- The current `src/MathLearning.Infrastructure/Services/Leaderboard/DbBackedRedisLeaderboardService.cs` implementation already avoids full ordered-id materialization.
- `GetUserRankAsync`:
  - loads the current user profile with `AsNoTracking()`;
  - builds a scope-restricted `IQueryable<UserProfile>`;
  - checks scope membership with `AnyAsync(...)`;
  - computes rank with `CountAsync(...)` over only the rows that score higher than the current user, using the period-specific tie-breaker.
- `GetNearRivalsAsync`:
  - reuses the same DB-side rank count;
  - fetches only a 5-row window with `OrderByScore(...).Skip(...).Take(5)`;
  - materializes only the near-rival slice, not the full scope.
- DB calls that remain required:
  - scope lookup for school/faculty/friends;
  - `AnyAsync` membership check;
  - `CountAsync` rank calculation;
  - `Skip/Take` page fetch for the rival window.
- Candidate for later work:
  - unify the DB fallback rank helper with the student leaderboard helper or move the comparator into a shared compiled-query path if this becomes a measured hotspot.
- Coverage already in place for this prompt:
  - `tests/MathLearning.Tests/Services/DbBackedRedisLeaderboardServiceTests.cs`
  - `tests/MathLearning.Tests/Endpoints/LeaderboardEndpointsIntegrationTests.cs`

---

### 3.5 Redis startup/cold-start behavior

Code signal:

- Redis is optional; without Redis the app falls back to DB-backed leaderboard.
- With a Redis connection string, startup now resolves bounded Redis options before creating the multiplexer and keeps the DB fallback if Redis is absent or misconfigured.

Risk:

```text
Misconfigured or slow Redis can hurt cold start or runtime resolution. Redis should be configured with bounded connect/retry behavior and explicit fallback policy.
```

Prompt: `BE-PERF-005`.

Implementation status:

- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs` now resolves Redis through a bounded `ConfigurationOptions` object before creating the multiplexer.
- Redis connection defaults now come from config keys:
  - `ConnectionStrings:Redis` or `Redis:ConnectionString`
  - `Redis:ConnectTimeoutMs` default `2000`
  - `Redis:SyncTimeoutMs` default `2000`
  - `Redis:ConnectRetry` default `3`
  - `Redis:KeepAliveSeconds` default `60`
  - `Redis:AbortOnConnectFail` default `false`
  - `Redis:DefaultDatabase` optional
- If Redis is not configured, the app still registers `DbBackedRedisLeaderboardService`.
- If Redis configuration fails at startup, the code logs the failure and also falls back to the DB-backed leaderboard service.
- Test evidence:
  - `tests/MathLearning.Tests/Infrastructure/RedisConfigurationOptionsTests.cs` verifies the default bounded values and explicit overrides.
- Manual smoke steps if needed:
  1. Start the API without Redis settings and confirm `/api/leaderboard` still responds through DB fallback.
  2. Start with a Redis connection string and intentionally low timeout values to confirm startup remains bounded and the configured values are reflected in logs.

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

Implementation status:

- `docs/BACKEND_ROUTE_COMPATIBILITY_AUDIT.md` now classifies canonical mobile routes, compatibility aliases, admin/internal support routes, and adjacent/future surfaces.
- Duplicate-work alias pairs are documented explicitly, including `/api/auth/login` vs `/auth/login`, `/api/quiz/batch-submit` vs `/api/quiz/offline-submit`, `/api/leaderboard/rivals` vs `/api/leaderboard/friends`, and the profile/avatar aliases.
- Route evidence is tied back to existing tests:
  - `tests/MathLearning.Tests/Endpoints/MobileApiRouteContractTests.cs`
  - `tests/MathLearning.Tests/Endpoints/MobileCompatibilityEndpointsIntegrationTests.cs`
  - `tests/MathLearning.Tests/Endpoints/LeaderboardEndpointsIntegrationTests.cs`
  - `tests/MathLearning.Tests/Endpoints/AuthDevSeedLoginTests.cs`
  - `tests/MathLearning.Tests/Endpoints/OfflineBatchSubmitCompatibilityTests.cs`
- Follow-up only: any legacy-alias removal should wait for consumer evidence and a dedicated removal prompt.

---

## 4. Priority stack

| Priority | Work | Why |
|---:|---|---|
| P0 | `BE-PERF-001` quiz start query budget | Directly affects first question latency. |
| P0 | `BE-PERF-002` Daily SRS query budget | Directly affects Daily Review and mobile redirect/cache flow. |
| P0 | `BE-PERF-003` answer/offline replay transaction audit | **Done** — see `docs/QUIZ_ANSWER_TRANSACTION_AUDIT.md`. |
| P0 | `BE-PERF-004` leaderboard rank fallback | Prevents DB fallback from becoming O(N). |
| P1 | `BE-PERF-005` Redis startup/fallback policy | Cold-start and reliability. |
| P1 | `BE-PERF-006` startup/background service budget | Cold-start and Fly/container behavior. |
| P1 | `BE-PERF-007` explicit performance budgets | Turns instrumentation into actionable guardrails. |
| P2 | `BE-PERF-008` route compatibility/deprecation audit | **Done** â€” see `docs/BACKEND_ROUTE_COMPATIBILITY_AUDIT.md`. |

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

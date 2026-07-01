# Backend request-path performance budgets

Last aligned: 2026-07-01  
Prompt: `BE-PERF-007`

## Goal

Turn existing Serilog + middleware + OpenTelemetry instrumentation into **explicit p95 budgets** for mobile-critical flows. Use these as staging smoke thresholds and regression guardrails (`docs/BACKEND_REGRESSION_GUARDRAILS.md` §3.6).

Targets assume PostgreSQL in-region, warm connection pool, no pending migrations, and typical mobile payload sizes. Adjust upward for cold cache, large offline batches, or DB-fallback leaderboard (Redis down).

## Observability sources

| Source | Location | Fields / signal | When emitted |
|---|---|---|---|
| Request performance log | `RequestPerformanceLoggingMiddleware` | `Method`, `Path`, `StatusCode`, `ElapsedMs`, `DbQueryCount` | Every HTTP request (finally block) |
| Serilog HTTP completion | `Program.cs` → `UseSerilogRequestLogging` | `RequestMethod`, `RequestPath`, `StatusCode`, `Elapsed`, `CorrelationId` | Every HTTP request |
| DB query counter | `PerformanceDbCommandInterceptor` | `HttpContext.Items["perf:db-query-count"]` | Per EF command inside request scope |
| OpenTelemetry trace | `ServiceRegistrationExtensions.AddObservabilityServices` | ASP.NET Core span + optional EF span | When OTLP/console exporter configured |
| Cold-start timing | `Program.cs` database block | `Database startup completed in {ElapsedMs}ms` | Once per process start |
| Runtime snapshot | `GET /metrics` | `uptimeSeconds`, `memoryMb`, `threadCount` | On demand |
| Admin log search | `GET /api/logs/search` | Filter `Message` contains `Request performance` | Admin-authenticated |

Middleware order (relevant for correlation):

```text
GlobalException → CorrelationId → RequestPerformanceLogging → SerilogRequestLogging → …
```

EF command counts include reader, scalar, and non-query commands issued through `ApiDbContext` during the request.

---

## Mobile-critical flow budgets

### Summary table

| Flow | Primary route(s) | p95 `ElapsedMs` | p95 `DbQueryCount` | Contract / perf tests |
|---|---|---:|---:|---|
| Quiz start | `POST /api/quiz/start` | ≤ 400 | ≤ 12 | `QuizStartContractIntegrationTests` |
| Legacy quiz questions | `GET /api/quiz/questions` | ≤ 450 | ≤ 14 | `QuizStartContractIntegrationTests` |
| SRS daily | `GET /api/quiz/srs/daily` | ≤ 350 | ≤ 10 | `SrsEndpointsIntegrationTests` |
| SRS mixed | `GET /api/quiz/srs/mixed` | ≤ 400 | ≤ 12 | `SrsEndpointsIntegrationTests` |
| Answer submit (fresh) | `POST /api/quiz/answer` | ≤ 500 | ≤ 18 | `QuizAnswerIdempotencyTests`, `MobileMutationContractIntegrationTests` |
| Answer submit (replay) | `POST /api/quiz/answer` (same keys) | ≤ 150 | ≤ 4 | `QuizAnswerIdempotencyTests` |
| Offline batch | `POST /api/quiz/offline-submit` | ≤ 800 + 50×N answers | ≤ 8 + 4×N | `OfflineBatchSubmitCompatibilityTests` |
| Leaderboard (Redis) | `GET /api/leaderboard/global` | ≤ 300 | ≤ 8 | `LeaderboardEndpointsIntegrationTests` |
| Leaderboard (DB fallback) | same, Redis unavailable | ≤ 600 | ≤ 15 | `DbBackedRedisLeaderboardServiceTests` |
| Progress overview | `GET /api/progress/overview` | ≤ 400 | ≤ 10 | Progress integration tests |
| Adaptive path | `GET /api/adaptive/path` | ≤ 500 | ≤ 15 | Adaptive endpoint tests |
| Auth login | `POST /api/auth/login` | ≤ 350 | ≤ 6 | Auth tests |
| Auth profile bootstrap | `GET /api/users/profile` | ≤ 200 | ≤ 5 | User/profile tests |
| Cold start (blocking DB) | pre-listen | ≤ 15 000 | N/A | `BACKEND_COLD_START_BUDGET.md` |

`N` = number of answers in offline batch payload.

### Per-flow notes

**Quiz start** (`BE-PERF-001`): bounded random selection + `AsNoTracking` question load. Breach usually means extra round-trips (session save, count+skip, or split-query fan-out).

**SRS daily / mixed** (`BE-PERF-002`): due scan uses indexed `UserId + NextReview` ordering; padding adds one bounded random fetch. Watch for duplicate question graph loads.

**Answer submit** (`BE-PERF-003`): serializable transaction by design. Replay path must stay ledger-only until mutation is required. See `docs/QUIZ_ANSWER_TRANSACTION_AUDIT.md`.

**Offline batch**: single serializable transaction; budget scales with batch size. Large mobile replay queues should be chunked client-side if p95 exceeds mobile timeout.

**Leaderboard** (`BE-PERF-004`): Redis path is cheaper; DB fallback rank uses `CountAsync`, not full ID materialization. `includeMe=true` adds rank query.

**Progress / adaptive**: read-mostly; prefer `AsNoTracking` and capped limits. Adaptive path may call weakness/recommendation services — treat spikes as P1 investigation, not P0 unless mobile blocks.

**Auth profile**: post-login bootstrap; should be a small number of profile + cosmetic projection reads.

**Startup**: not a request budget — see [`BACKEND_COLD_START_BUDGET.md`](BACKEND_COLD_START_BUDGET.md).

---

## Log / trace mapping (how to read evidence)

### 1. Request performance line (primary budget check)

```text
Request performance. Method=POST Path=/api/quiz/start StatusCode=200 ElapsedMs=142.5 DbQueryCount=9
```

Filter locally (PowerShell):

```powershell
Select-String -Path "logs\mathlearning-*.log" -Pattern "Request performance.*Path=/api/quiz/start"
```

Filter in admin API:

```http
GET /api/logs/search?searchTerm=Request%20performance%20Path%3D%2Fapi%2Fquiz%2Fstart&limit=50
```

### 2. Serilog HTTP line (wall-clock cross-check)

```text
HTTP POST /api/quiz/start responded 200 in 145.3200 ms
```

Includes `CorrelationId` in structured properties when using JSON sink. Compare with `ElapsedMs` above; small delta is normal (middleware ordering).

### 3. OpenTelemetry

| Config key | Effect |
|---|---|
| `OpenTelemetry:ServiceName` | Trace service name (default `mathlearning-api`) |
| `OpenTelemetry:EnableEntityFrameworkInstrumentation` | EF spans; default **on** outside Development |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Export traces to collector |
| Development | Console trace exporter enabled |

Look for `Microsoft.AspNetCore` span duration per route and child `Microsoft.EntityFrameworkCore` spans when EF instrumentation is on.

### 4. Startup

```text
Database startup completed in 2341ms. StartupMode=AutoMigrate
```

Pair with `GET /health/ready` and `GET /health/schema`.

---

## Local / staging smoke procedure

### Prerequisites

- API running (`dotnet run --project src/MathLearning.Api`)
- Valid JWT (login or test user seed)
- Log file path from Serilog config (default rolling file under `logs/`)

### Step 1 — warm pool

```bash
curl -s http://localhost:5000/health/ready
```

### Step 2 — hit mobile-critical routes

Replace `$TOKEN` with a bearer token.

```bash
# Quiz start
curl -s -o /dev/null -w "%{http_code} %{time_total}\n" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"count":10,"difficulty":"medium"}' \
  http://localhost:5000/api/quiz/start

# SRS daily
curl -s -o /dev/null -w "%{http_code} %{time_total}\n" \
  -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/quiz/srs/daily?limit=20"

# Progress overview
curl -s -o /dev/null -w "%{http_code} %{time_total}\n" \
  -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/progress/overview

# Leaderboard
curl -s -o /dev/null -w "%{http_code} %{time_total}\n" \
  -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/leaderboard/global?period=week&limit=50"

# Adaptive path
curl -s -o /dev/null -w "%{http_code} %{time_total}\n" \
  -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/adaptive/path

# Profile bootstrap
curl -s -o /dev/null -w "%{http_code} %{time_total}\n" \
  -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/users/profile
```

`time_total` is a coarse curl wall clock; use log `ElapsedMs` + `DbQueryCount` for authoritative evidence.

### Step 3 — extract p95 from logs

PowerShell example (last 100 quiz-start lines):

```powershell
Select-String -Path "logs\mathlearning-*.log" -Pattern "Request performance.*Path=/api/quiz/start" |
  Select-Object -Last 100 |
  ForEach-Object {
    if ($_.Line -match 'ElapsedMs=([\d.]+).*DbQueryCount=(\d+)') {
      [pscustomobject]@{ ElapsedMs = [double]$matches[1]; DbQueryCount = [int]$matches[2] }
    }
  } | Measure-Object -Property ElapsedMs -Average -Maximum
```

Repeat per route path. Flag any sample where `ElapsedMs` or `DbQueryCount` exceeds the budget table.

### Step 4 — regression tests (CI substitute)

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~QuizStart|SrsEndpoints|QuizAnswerIdempotency|OfflineBatch|Leaderboard|Progress|Adaptive"
```

Passing contract tests do not replace staging log review, but they guard response shape and idempotency while query budgets are monitored manually.

### Step 5 — staging Fly check

After deploy:

1. `curl https://<app>/health/ready`
2. Run Step 2 against staging URL
3. Search Fly logs / admin log API for `Request performance` lines with `StatusCode=200`
4. Confirm no systematic `DbQueryCount` regression vs this document

---

## Breach response

| Severity | Condition | Action |
|---|---|---|
| P0 | Quiz start or SRS daily p95 > 2× budget | Block release; profile EF queries in endpoint |
| P1 | Leaderboard DB fallback > budget | Verify Redis health; profile `DbBackedRedisLeaderboardService` |
| P1 | Answer replay > 150 ms | Check idempotency ledger path still short-circuits |
| P2 | Progress/adaptive > budget | Optimize reads; not a mobile boot blocker |

Document breaches in PR description with log snippets (`Path`, `ElapsedMs`, `DbQueryCount`, `CorrelationId`).

---

## Related docs

- [`BACKEND_COLD_START_BUDGET.md`](BACKEND_COLD_START_BUDGET.md) — startup budgets
- [`QUIZ_ANSWER_TRANSACTION_AUDIT.md`](QUIZ_ANSWER_TRANSACTION_AUDIT.md) — answer/offline TX semantics
- [`BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md`](BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md) — findings stack
- [`SERILOG_LOGGING_GUIDE.md`](../SERILOG_LOGGING_GUIDE.md) — sinks and admin log API

## Next prompt

`BE-PERF-008` — route compatibility / endpoint bloat audit.

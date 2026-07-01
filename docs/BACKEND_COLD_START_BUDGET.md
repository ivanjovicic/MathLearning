# Backend cold-start and background-service budget

Last aligned: 2026-07-01  
Prompt: `BE-PERF-006`

## Goal

Keep API cold-start predictable on Fly/containers: blocking work finishes before `app.Run`, background work does not delay readiness, and `/health/ready` reflects real schema state.

## Startup timeline

| Phase | When | Blocking? | Budget (p95 target) | Evidence |
|---|---|---|---|---|
| Serilog + `WebApplication.CreateBuilder` | Pre-build | Yes | < 500 ms | Startup log line |
| DI registration (`AddDatabaseServices`, observability, cache) | `builder` phase | Yes | < 2 s | Local profile |
| Postgres reachability probe for Hangfire | `AddBackgroundJobServices` | Yes | < 3 s | Warning log if skipped |
| Redis `ConnectionMultiplexer.Connect` | `AddCacheAndInfrastructureServices` | Yes (bounded) | ≤ 2 s connect (`Redis:ConnectTimeoutMs` default) | `RedisConfigurationOptionsTests` |
| Hangfire storage + server registration | `AddBackgroundJobServices` | Yes when PG up | < 2 s | `/health/background-jobs` |
| **Database migrate / schema guard** | After `app.Build`, pre-listen | **Yes** | < 15 s prod (no pending migrations) | `/health/ready`, `/health/schema` |
| Cosmetic catalog seed (`CosmeticStartupSeeder`) | Inside DB startup block | Yes | < 1 s | Idempotent upsert |
| Design tokens init (`EnsureInitializedAsync`) | Inside DB startup block | Yes | < 2 s | `GET /api/ui/tokens` |
| Content seed (`DbSeeder`) | After schema OK, optional | Yes when enabled | < 30 s dev only | `SeedContent:Enabled` |
| Admin / test account seeders | After schema OK | Yes when enabled | < 5 s dev/test | Env-gated |
| Middleware + endpoint mapping | Pre-listen | Yes | < 1 s | — |
| Hangfire recurring job registration | Pre-listen, if HF enabled | Yes | < 1 s | Log warning when skipped |
| **Hosted services start** | With `app.Run` | **No** (async) | First loop deferred | See below |
| First HTTP request | Runtime | — | — | Serilog request timing |

`Database:StartupMode=Skip` (tests) bypasses the blocking DB block entirely.

## Hosted services (non-blocking after listen)

| Service | First work | Blocks readiness? | Notes |
|---|---|---|---|
| `IndexMaintenanceBackgroundService` | Waits until 03:00 UTC | No | Safe |
| `XpResetBackgroundService` | Hourly delay, then DB scan | No | First reset after 1 h |
| `WeaknessAnalysisScheduler` | Internal schedule | No | Analytics only |
| `WeaknessAnalysisDailyHostedService` | Daily schedule | No | Analytics only |
| Hangfire server | Polls queues | No | Disabled when PG down at startup |

## Health endpoints

| Route | Purpose |
|---|---|
| `GET /health` | Liveness |
| `GET /health/ready` | Readiness (DB + schema alignment) |
| `GET /health/schema` | Schema migration evidence |
| `GET /health/background-jobs` | Hangfire enabled vs fallback |
| `GET /metrics` | Uptime/memory snapshot |

## Safe deferral candidates (not implemented)

Do **not** move without explicit prompt + readiness test updates:

1. `CosmeticStartupSeeder` — mobile cosmetics depend on catalog at first request; keep in blocking path until lazy-init is proven.
2. `DesignTokenQueryService.EnsureInitializedAsync` — admin UI tokens; could move post-ready with cache miss penalty.
3. `DbSeeder` content seed — already optional via `SeedContent:Enabled`; keep off in production.

## Redis policy (see BE-PERF-005)

Config keys in `appsettings.json` → `Redis:*`:

- `ConnectTimeoutMs` (default 2000)
- `SyncTimeoutMs` (default 2000)
- `ConnectRetry` (default 3)
- `KeepAliveSeconds` (default 60)
- `AbortOnConnectFail` (default false — allows lazy reconnect)

Failure → `DbBackedRedisLeaderboardService` scoped fallback; startup continues.

## Staging smoke checklist

```bash
# 1. Cold start logs — look for Database startup completed in {ElapsedMs}ms
dotnet run --project src/MathLearning.Api

# 2. Readiness
curl -s http://localhost:5000/health/ready | jq .

# 3. Background jobs state
curl -s http://localhost:5000/health/background-jobs | jq .

# 4. Schema evidence
curl -s http://localhost:5000/health/schema | jq .
```

## Residual risks

- Pending migrations in production still block startup by design (`VerifyOnly` / production mode).
- Hangfire disabled when PG probe fails at boot — recurring jobs not registered until restart.
- Redis connect at DI time can add up to `ConnectTimeoutMs * ConnectRetry` wall time before fallback.

## Next prompt

`BE-PERF-007` — explicit request-path performance budgets and observability mapping.

# Backend Architecture Overview

Last aligned: 2026-06-25  
Repo: `ivanjovicic/MathLearning`

This is the token-saving architecture map for agents. It explains what owns what, where to inspect first, and which invariants must not be broken.

---

## 1. Runtime startup map

`src/MathLearning.Api/Program.cs` is the runtime composition root.

Startup responsibilities:

- Serilog setup
- environment/port binding
- database connection validation
- database services and schema guard
- background job/Hangfire setup
- application/infrastructure services
- JWT/security services
- Swagger/API docs
- middleware: exception handler, correlation id, request performance logging, forwarded headers, CORS, rate limit, auth/authorization
- health, metrics, endpoint mapping, static uploaded avatars, Hangfire recurring jobs

Endpoint mapping order is visible in `Program.cs` and currently includes:

- health and metrics
- auth
- controllers
- users
- quiz and SRS
- question authoring
- sync
- idempotency observability
- adaptive
- practice sessions
- analytics
- explanations
- hints
- coins legacy
- powerups
- economy settlement
- cosmetics
- progress
- Daily Run
- leaderboard
- avatar legacy/compatibility
- maintenance
- monitoring/logging
- bug endpoints

---

## 2. Project layout

| Area | Path | Purpose |
|---|---|---|
| API host | `src/MathLearning.Api` | ASP.NET Core app, endpoint mapping, middleware, startup extensions. |
| API endpoints | `src/MathLearning.Api/Endpoints` | Minimal API route definitions and HTTP boundary. |
| Application | `src/MathLearning.Application` | DTOs, validators, app services, use-case style logic. |
| Domain | `src/MathLearning.Domain` | Entities and domain events. |
| Infrastructure | `src/MathLearning.Infrastructure` | EF Core, persistence, external services, Redis/leaderboard, idempotency services. |
| Translation job | `src/MathLearning.TranslationJob` | Background translation job. |
| Admin | `src/MathLearning.Admin` | Admin app surface. |
| Tests | `tests/MathLearning.Tests` | Unit, integration, endpoint, contract, idempotency tests. |
| Docs | `docs/` | Backend architecture, endpoint inventory, handoffs, evidence. |

---

## 3. Endpoint ownership rule

Endpoint files should remain thin HTTP boundaries.

Endpoint responsibilities:

- route definition
- auth/user resolution
- request binding and minimal validation
- idempotency begin/replay/conflict wrapper where needed
- service call
- response shaping

Avoid moving complex business rules into endpoint lambdas if a service already owns the behavior.

---

## 4. Auth and user identity

The backend uses authenticated server user id as the authority for user-owned mutations.

Common patterns:

- `ctx.User.FindFirst("userId")?.Value`
- `EndpointUser.GetUserId(ctx)`
- route `userId` must match auth user for user-owned routes
- admin endpoints may act on another target user only behind admin policy

Do not use request body `userId` as mutation authority.

---

## 5. Idempotency architecture

The backend intentionally uses a mixed-but-documented idempotency storage model.

| Flow | Storage / service | Scope / policy |
|---|---|---|
| Quiz answer | shared `IdempotencyLedger` / `IIdempotencyLedgerService` | `userId + operationType + operationId/idempotencyKey` |
| SRS update | shared `IdempotencyLedger` / `IIdempotencyLedgerService` | `userId + operationType + operationId/idempotencyKey` |
| Economy / seasons / shop | `economy_transactions` / `IEconomyTransactionService` | `userId + transactionType + operationId/idempotencyKey` |
| Cosmetics claim/grant | `cosmetics_idempotency_ledger` / `ICosmeticsIdempotencyService` | per-user operation/idempotency key semantics |
| Daily Run chest | `daily_run_chest_claims` / `DailyRunChestClaimIdempotency` | Policy B: replay by transaction/day, not generic payload conflict |

Do not introduce a new idempotency pattern without updating:

- `AGENTS.md`
- `docs/mobile_contract_idempotency_handoff.md`
- `docs/backend_contract_gap_report.md`
- `docs/ARCHITECTURE_OVERVIEW.md`
- tests under `tests/MathLearning.Tests/Idempotency` and/or `tests/MathLearning.Tests/Contracts`

---

## 6. Persistence and migrations

Primary EF Core context:

```text
src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs
```

Migration path:

```text
src/MathLearning.Infrastructure/Migrations/
```

Startup schema behavior is environment-sensitive. Development can auto-migrate depending on startup mode; higher environments must be treated carefully. Do not assume production will auto-apply migrations.

Migration rules:

- inspect existing migrations first
- preserve existing data unless destructive migration is explicitly requested
- add unique indexes for idempotency scope
- update docs when schema becomes part of mobile/backend contract

---

## 7. Testing architecture

Important test buckets:

| Bucket | Path | Protects |
|---|---|---|
| Idempotency | `tests/MathLearning.Tests/Idempotency` | duplicate/conflict/rollback/user isolation |
| Mobile contracts | `tests/MathLearning.Tests/Contracts` | route/auth/request/response behavior for mobile consumers |
| Endpoints | `tests/MathLearning.Tests/Endpoints` | route-level behavior, ownership, auth, integration |
| Services | `tests/MathLearning.Tests/Services` | domain/application/infrastructure services |
| Helpers | `tests/MathLearning.Tests/Helpers` | test factories/auth/db setup |

Validation rule: name the exact test command and result. Do not claim CI green without Actions evidence.

---

## 8. Background jobs and operations

The API can register Hangfire recurring jobs when Hangfire is enabled and database startup succeeds.

Current recurring job families include:

- practice daily aggregation
- school leaderboard refresh
- school leaderboard snapshots
- anti-cheat ML review sweep

Health endpoints expose whether background jobs are enabled at startup.

---

## 9. Observability

Current observability layers:

- Serilog structured logs in production
- correlation id middleware
- request performance logging middleware
- health/readiness/schema endpoints
- minimal `/metrics`
- idempotency observability endpoints/services

Do not log raw answer payloads, tokens, emails, passwords, or full idempotency payload JSON.

---

## 10. Canonical vs legacy route policy

Canonical mobile routes should be preferred for new work.

Examples:

- use `/api/economy/*`, not legacy `/api/coins/*`
- use `/api/cosmetics/*`, not legacy avatar/cosmetic mutation routes
- use `/api/quiz/srs/update` for SRS update
- keep Daily Run chest policy documented as its own domain-table idempotency flow

Legacy routes may remain for compatibility, but do not expand them unless explicitly requested.

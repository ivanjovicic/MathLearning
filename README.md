# MathLearning

MathLearning is the backend repository for the MathLearning platform. It provides the ASP.NET Core API,
persistence, background jobs, and backend contracts used by the Flutter mobile app in
`ivanjovicic/Mathlearning-Mobile-App`.

The current stabilization focus is **mobile/backend contract parity and idempotent backend settlement** for
offline replay, Daily Run rewards, seasons, economy, and cosmetics.

---

## Repository role

| Area | Responsibility |
|---|---|
| API | REST endpoints consumed by the Flutter mobile app |
| Application logic | Quiz, SRS, progress, Daily Run, season, economy, cosmetics, leaderboard flows |
| Persistence | PostgreSQL / EF Core migrations and data access |
| Cache / leaderboard | Redis-backed fast reads where applicable |
| Integration tests | Backend proof for route, auth, idempotency, transaction, and contract behavior |

---

## Mobile contract handoff

Before changing backend endpoints consumed by the mobile app, read:

- [`docs/mobile_contract_idempotency_handoff.md`](docs/mobile_contract_idempotency_handoff.md) — backend verification checklist for the mobile contract
- [`docs/backend_contract_gap_report.md`](docs/backend_contract_gap_report.md) — current backend gap snapshot against the mobile handoff
- Mobile repo: `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_api_contract.md`
- Mobile repo: `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_backend_contract_status.md`
- Mobile repo: `ivanjovicic/Mathlearning-Mobile-App/docs/backend_idempotency_implementation_plan.md`
- Mobile repo: `ivanjovicic/Mathlearning-Mobile-App/docs/RELEASE_CHECKLIST.md`

Do not mark a backend-facing mobile feature production-safe from mobile tests alone. Backend endpoint code,
migrations, and integration/contract tests in this repo must prove the behavior.

---

## Current P0 backend verification focus

Default idempotency scope for authenticated retryable mutations:

```text
userId + operationType + operationId/idempotencyKey
```

P0 endpoints to verify first:

| Endpoint | Operation type |
|---|---|
| `POST /api/quiz/answer` | `quiz_answer` |
| `POST /api/quiz/srs/update` | `srs_update` |
| `POST /api/daily-run/chest/claim` | `daily_run_chest_claim` |
| `POST /api/seasons/daily-run-claim` | `season_daily_run_claim` |
| `POST /api/seasons/milestones/{milestoneId}/claim` | `season_milestone_claim` |
| `POST /api/cosmetics/fragments/grant` | `cosmetics_fragment_grant` |
| `POST /api/cosmetics/items/{itemKey}/claim` | `cosmetics_item_claim` |
| `POST /api/economy/coins/spend` | `economy_coin_spend` |
| `POST /api/economy/hints/use` | `economy_hint_use` |
| `POST /api/economy/rewards/claim` | `economy_reward_claim` |
| `POST /api/shop/streak-freeze/purchase` | `shop_streak_freeze_purchase` |

For exact behavior and test expectations, see [`docs/mobile_contract_idempotency_handoff.md`](docs/mobile_contract_idempotency_handoff.md). For current implementation gaps, see [`docs/backend_contract_gap_report.md`](docs/backend_contract_gap_report.md).

---

## Tech stack

- Backend: ASP.NET Core / .NET
- ORM: Entity Framework Core
- Database: PostgreSQL
- Cache / leaderboard: Redis where enabled
- Tests: backend unit/integration tests under `tests/`
- Mobile consumer: Flutter app in `ivanjovicic/Mathlearning-Mobile-App`

---

## Useful folders

| Folder | Purpose |
|---|---|
| `src/MathLearning.Api` | API project / HTTP endpoints |
| `src/MathLearning.Application` | Application/business logic |
| `src/MathLearning.Infrastructure` | Persistence, caching, external integrations |
| `src/MathLearning.TranslationJob` | Background translation job |
| `tests/MathLearning.Tests` | Backend tests |
| `scripts/` | Local setup / migration helper scripts, if present |
| `docs/` | Backend documentation and mobile contract handoff |

---

## Local development

From repository root:

```powershell
dotnet restore
dotnet build
dotnet run --project src\MathLearning.Api\MathLearning.Api.csproj
```

Run tests:

```powershell
dotnet test
```

If database/Redis scripts are present under `scripts/`, prefer those for local infrastructure setup. Keep this
README aligned with actual script names when scripts change.

---

## Backend verification checklist

Before updating the mobile repo status matrix, backend work should have evidence for:

- endpoint route and auth behavior
- EF migration / schema change when needed
- domain mutation and idempotency ledger in the same transaction
- duplicate same-payload replay behavior
- conflict behavior for same key with different payload
- rollback behavior when mutation fails
- integration/contract tests committed in this repo

Then update:

```text
ivanjovicic/Mathlearning-Mobile-App/docs/mobile_backend_contract_status.md
```

with backend PR/commit/test evidence.

---

## Contributing

Keep backend endpoint changes synchronized with the mobile contract. If a route, payload shape, or idempotency
behavior changes, update backend tests and the mobile repo documentation in the same workstream.

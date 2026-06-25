# MathLearning Backend - AI Agent Rulebook

This file defines the working rules for AI-assisted changes in `ivanjovicic/MathLearning`.

Read this before changing backend code or docs. The goal is to save tokens, avoid rediscovery, and prevent recurring mistakes around auth scope, idempotency, migrations, and mobile contract drift.

---

## 1. Start here

For almost every backend task, read in this order:

1. `AGENTS.md` — this rulebook.
2. `docs/DOCS_INDEX.md` — which docs are canonical/status/reference.
3. `docs/AGENT_QUICKSTART.md` — shortest safe path for common tasks.
4. `docs/ARCHITECTURE_OVERVIEW.md` — project layout and runtime map.
5. `docs/API_ENDPOINT_INVENTORY.md` — current endpoint map.
6. `docs/backend_contract_gap_report.md` — latest backend/mobile contract evidence.
7. `docs/mobile_contract_idempotency_handoff.md` — idempotency handoff from the mobile contract.

Do not reread the whole repository unless the task truly requires it.

---

## 2. Repository role

This repo is the ASP.NET Core backend for the MathLearning Flutter mobile app.

The backend owns:

- auth, profiles, progress, quiz, SRS, practice, Daily Run, economy, cosmetics, leaderboard, sync, health, logging, and maintenance endpoints
- EF Core persistence and migrations
- backend-authoritative reward/economy/cosmetics settlement
- idempotency ledgers and duplicate/conflict behavior for retryable mobile mutations
- integration/contract tests proving mobile/backend behavior

The mobile contract lives in `ivanjovicic/Mathlearning-Mobile-App`. Do not invent mobile payloads; verify against mobile docs and tests.

---

## 3. Architecture rules

- `src/MathLearning.Api/Program.cs` wires startup, middleware, health, Swagger, CORS, auth, and endpoint mapping.
- Endpoint files live in `src/MathLearning.Api/Endpoints/`.
- Application/service behavior lives under `src/MathLearning.Application/` and `src/MathLearning.Infrastructure/`.
- Domain entities live under `src/MathLearning.Domain/`.
- EF Core context and migrations live under `src/MathLearning.Infrastructure/Persistance/`.
- Tests live under `tests/MathLearning.Tests/`.

Do not put business logic directly into endpoints if a service already owns it. Endpoints should resolve auth user, bind/validate request, call the owning service/ledger, and return contract-shaped results.

---

## 4. Auth and user-scope rules

- Mobile-facing mutations must scope writes by authenticated server user id.
- Never trust `userId` from request body as mutation authority.
- Route `userId` is acceptable only when it must equal the authenticated user or the endpoint is explicitly admin-only.
- Admin-targeted routes must keep actor user and target user separate.
- Add or update tests when changing cross-user or ownership behavior.

Important test areas:

- `MutationUserScopeIntegrationTests.cs`
- `UserSettingsEndpointsIntegrationTests.cs`
- `PracticeSessionServiceIntegrationTests.cs`
- `SyncServiceTests.cs`

---

## 5. Idempotency rules

Retryable mobile mutations must use stable operation keys.

Default handoff scope:

```text
userId + operationType + operationId/idempotencyKey
```

Current backend uses a deliberate mixed storage strategy:

- shared `IdempotencyLedger` for Quiz/SRS-style mutations
- `economy_transactions` for economy and season settlement
- `cosmetics_idempotency_ledger` for cosmetics claim/grant flows
- Daily Run chest domain-table policy via `daily_run_chest_claims`

Do not create a fourth pattern without documenting why.

Required behavior for generic idempotent mutations:

- first request mutates once and stores result
- duplicate same payload replays settled result / `alreadyProcessed` / `alreadyClaimed`
- same keys with different payload returns `409 idempotency_conflict`
- domain failure must not leave a completed ledger row
- different users are isolated

Daily Run chest is a documented exception: it uses domain-table Policy B and replays by transaction/day rather than generic payload conflict.

---

## 6. Endpoint and mobile contract rules

- Keep endpoint changes synchronized with `docs/API_ENDPOINT_INVENTORY.md`.
- Keep mobile-facing payload/behavior aligned with `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_api_contract.md`.
- Do not expand legacy routes such as `/api/coins/*` or legacy avatar/cosmetics routes unless the task explicitly targets compatibility.
- Prefer canonical mobile routes:
  - `/api/economy/*` over `/api/coins/*`
  - `/api/cosmetics/*` over legacy `/api/avatar/*` mutation paths
  - `/api/quiz/srs/update` for SRS update
  - `/api/daily-run/chest/claim` for Daily Run chest settlement

---

## 7. Migration and database safety

- Inspect existing migrations and DbContext mappings before adding a migration.
- Make unique indexes match idempotency scope exactly.
- Ledger write and domain mutation should be in the same database transaction where applicable.
- Do not assume production can auto-migrate. Startup mode and environment determine migration behavior.
- Document new tables/indexes in `docs/backend_contract_gap_report.md` or the relevant architecture doc.

---

## 8. Testing and validation

Use the narrowest test command that proves the change first. Then run broader tests if the touched area warrants it.

Common test groups:

- Idempotency: `tests/MathLearning.Tests/Idempotency/*`
- Mobile HTTP contract: `tests/MathLearning.Tests/Contracts/*`
- Endpoint route/auth: `tests/MathLearning.Tests/Endpoints/*`
- Services: `tests/MathLearning.Tests/Services/*`

Do not claim CI is green unless a GitHub Actions run was found and checked. If no run is available, write: `No GitHub Actions evidence found via connector`.

---

## 9. Observability and logs

- Do not log raw answers, tokens, emails, passwords, or full request payload JSON.
- Idempotency observability should record safe metadata only: endpoint, operation type, safe operation suffix/hash, status, and category.
- Admin/observability endpoints must remain protected by the correct policy.

---

## 10. Commit and push policy

Default workflow is one completed prompt = committed and pushed work on `main` before the final response.

Exceptions:

- validation fails and the task cannot be fixed in scope
- user explicitly asks for branch/PR instead of direct main push
- repository protection blocks direct push

Final response must include:

- commit SHA(s)
- files changed
- validation run or reason skipped
- residual risks / next prompt

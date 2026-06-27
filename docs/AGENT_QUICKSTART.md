# Backend Agent Quickstart

Last aligned: 2026-06-27  
Repo: `ivanjovicic/MathLearning`

Use this document to avoid wasting tokens. Pick the section matching the task, read only the listed files first, then inspect deeper only if needed.

---

## 1. Universal preflight

Before editing:

1. Confirm target repo is `ivanjovicic/MathLearning`.
2. Read `AGENTS.md`.
3. Read `docs/DOCS_INDEX.md`.
4. Read `docs/BACKEND_REGRESSION_GUARDRAILS.md`.
5. Check whether the task touches mobile contract, idempotency, auth scope, migrations, endpoint routes, startup/deploy config, performance hot paths, UTF-8/localization, admin Blazor, or question authoring.
6. Name the historical bug class protected by the change before editing.
7. Inspect the exact files you will change.
8. Plan the narrowest useful test command.

Implementation prompts must include a `Backend regression guardrails` block with: historical bug class, why the change can reintroduce it, files inspected, validation planned, contract/schema/docs touched, and residual risk if validation cannot run.

Default final response:

```text
Commit: <sha>
Changed: <files>
Historical guardrail: <bug class>
Validation: <command/result or skipped reason>
Residual risk: <brief>
Next: <next prompt/task>
```

---

## 2. If task touches Quiz answer idempotency

Read:

- `docs/BACKEND_REGRESSION_GUARDRAILS.md` sections `idempotency-offline-replay`, `auth-user-scope`, and `mobile-contract-shape`
- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- `src/MathLearning.Api/Endpoints/SrsEndpoints.cs`
- `src/MathLearning.Infrastructure/Services/Idempotency/*`
- `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs`
- `tests/MathLearning.Tests/Contracts/MobileMutationContractIntegrationTests.cs`
- `docs/mobile_contract_idempotency_handoff.md`
- `docs/backend_contract_gap_report.md`

Protect:

- `operationId` / `idempotencyKey`
- `409 idempotency_conflict`
- duplicate replay response
- rollback without completed ledger row
- authenticated user scope

---

## 3. If task touches SRS

Read:

- `docs/BACKEND_REGRESSION_GUARDRAILS.md` sections `idempotency-offline-replay`, `performance-query-shape`, and `mobile-contract-shape`
- `src/MathLearning.Api/Endpoints/SrsEndpoints.cs`
- `src/MathLearning.Application/Services/*Srs*`
- `tests/MathLearning.Tests/Idempotency/SrsUpdateIdempotencyTests.cs`
- `tests/MathLearning.Tests/Contracts/MobileMutationContractIntegrationTests.cs`

Protect:

- duplicate update must not advance review state twice
- same keys / different payload must conflict
- no request-body user id authority

---

## 4. If task touches economy/rewards/coins/hints/streak-freeze/seasons

Read:

- `docs/BACKEND_REGRESSION_GUARDRAILS.md` sections `idempotency-offline-replay`, `auth-user-scope`, and `mobile-contract-shape`
- `src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs`
- economy services under `src/MathLearning.Application/Services/` and `src/MathLearning.Infrastructure/Services/`
- `tests/MathLearning.Tests/Idempotency/EconomyOperationIdIdempotencyTests.cs`
- `tests/MathLearning.Tests/Contracts/MobileEconomyContractIntegrationTests.cs`
- `tests/MathLearning.Tests/Contracts/MobileMutationContractIntegrationTests.cs`

Protect:

- `economy_transactions` scope: user + transaction type + operation/idempotency keys
- no double coin debit
- no double reward settlement
- `operationId` must be wired when request provides it

---

## 5. If task touches cosmetics/avatar

Read:

- `docs/BACKEND_REGRESSION_GUARDRAILS.md` sections `idempotency-offline-replay`, `mobile-contract-shape`, and `auth-user-scope`
- `src/MathLearning.Api/Endpoints/CosmeticsEndpoints.cs`
- `src/MathLearning.Api/Endpoints/AvatarEndpoints.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/*`
- `tests/MathLearning.Tests/Contracts/MobileCosmeticsContractIntegrationTests.cs`
- `tests/MathLearning.Tests/Contracts/MobileCosmeticsApiIntegrationTests.cs`
- `tests/MathLearning.Tests/Idempotency/CosmeticsMutationResponseTests.cs`

Protect:

- canonical mobile routes are `/api/cosmetics/*`
- legacy `/api/avatar/*` mutation paths must not become new mobile contract
- inventory response must reflect persisted state after mutation
- avatar PUT rejects unowned items
- fragment grants and item claims stay idempotent

---

## 6. If task touches Daily Run chest

Read:

- `docs/BACKEND_REGRESSION_GUARDRAILS.md` section `idempotency-offline-replay`
- `src/MathLearning.Api/Endpoints/DailyRunEndpoints.cs`
- `tests/MathLearning.Tests/Idempotency/DailyRunChestClaimIdempotencyTests.cs`
- `tests/MathLearning.Tests/Endpoints/DailyRunChestClaimEndpointTests.cs`
- `tests/MathLearning.Tests/Contracts/MobileMutationContractIntegrationTests.cs`

Protect:

- Policy B domain-table idempotency
- replay by transaction/day
- no generic payload-conflict requirement unless policy changes
- `daily_run_chest_claims` unique constraints and concurrency behavior

---

## 7. If task touches auth/user scope/security

Read:

- `docs/BACKEND_REGRESSION_GUARDRAILS.md` section `auth-user-scope`
- `src/MathLearning.Api/Endpoints/UserEndpoints.cs`
- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `tests/MathLearning.Tests/Endpoints/MutationUserScopeIntegrationTests.cs`
- `tests/MathLearning.Tests/Endpoints/UserSettingsEndpointsIntegrationTests.cs`
- `tests/MathLearning.Tests/Services/PracticeSessionServiceIntegrationTests.cs`
- `tests/MathLearning.Tests/Services/SyncServiceTests.cs`
- `docs/backend_contract_gap_report.md` U2 section

Protect:

- mutation user id comes from auth context
- route user id must match auth user unless admin-only
- sync operation payload user mismatch is rejected
- practice sessions are owned by authenticated user

---

## 8. If task touches endpoints/routes

Read:

- `docs/BACKEND_REGRESSION_GUARDRAILS.md` sections `mobile-contract-shape` and `auth-user-scope`
- `src/MathLearning.Api/Program.cs`
- relevant `src/MathLearning.Api/Endpoints/*.cs`
- `docs/API_ENDPOINT_INVENTORY.md`
- route/contract tests under `tests/MathLearning.Tests/Endpoints` and `tests/MathLearning.Tests/Contracts`

After route changes:

- update `docs/API_ENDPOINT_INVENTORY.md`
- update mobile contract/status docs if mobile-facing
- add route/auth smoke coverage if missing

---

## 9. If task touches migrations/schema

Read:

- `docs/BACKEND_REGRESSION_GUARDRAILS.md` section `schema-migration-drift`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- `src/MathLearning.Infrastructure/Migrations/*`
- affected entity files under `src/MathLearning.Domain/Entities/`
- `docs/ARCHITECTURE_OVERVIEW.md`

Rules:

- do not invent schema names without checking existing migrations
- match unique indexes to idempotency scope
- document new tables/indexes in gap report or architecture docs
- avoid destructive operations unless explicitly requested and tested

---

## 10. If task touches startup/deploy/config

Read:

- `docs/BACKEND_REGRESSION_GUARDRAILS.md` section `startup-deploy-config`
- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- `src/MathLearning.Admin/Program.cs` when Admin is involved
- deployment docs

Protect:

- production database target is explicit
- Hangfire disabled mode is safe and visible
- DataProtection keys persist across container restarts
- readiness endpoints reflect schema/runtime state honestly

---

## 11. If task touches performance/query shape

Read:

- `docs/BACKEND_REGRESSION_GUARDRAILS.md` section `performance-query-shape`
- `docs/BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md`
- endpoint/service owning the hot path
- relevant tests for quiz/SRS/leaderboard/progress/admin list pages

Protect:

- no full-table random ordering on hot paths
- no N+1 query loops in grids or mobile reads
- read-only queries are no-tracking where safe
- use projections when only DTO fields are required
- cap limits/page sizes

---

## 12. If task touches admin UI / Blazor / Identity

Read:

- `docs/BACKEND_REGRESSION_GUARDRAILS.md` sections `admin-blazor-auth-ui`, `utf8-localization`, and `compiler-warning-cleanliness`
- touched Admin Razor pages/components
- `src/MathLearning.Admin/Program.cs`
- relevant Identity/Admin tests if present

Protect:

- cookie auth mutations follow lifecycle-safe server flow
- returnUrl is local-only
- Identity concurrency stamp operations use fresh entities
- MudBlazor controls are preferred for forms/tables
- Serbian copy stays valid UTF-8

---

## 13. If task touches question authoring/math content

Read:

- `docs/BACKEND_REGRESSION_GUARDRAILS.md` section `question-authoring-integrity`
- `src/MathLearning.Api/Endpoints/QuestionAuthoringEndpoints.cs`
- `src/MathLearning.Application/Validators/QuestionAuthoringValidators.cs`
- Admin question pages/components/helpers
- question authoring tests

Protect:

- MCQ correctness uses stable option identity where possible
- server-side validation matches critical Admin UI rules
- option/step order remains stable
- sanitization/preview rules stay consistent
- hidden invalid combinations are rejected by backend tests

---

## 14. If task is docs-only

Read:

- `AGENTS.md`
- `docs/DOCS_INDEX.md`
- `docs/BACKEND_REGRESSION_GUARDRAILS.md`
- directly related docs
- exact source files referenced by the doc

Validation:

- verify file paths and test names exist
- do not claim tests passed if not run
- do not mark backend verified without code/test evidence

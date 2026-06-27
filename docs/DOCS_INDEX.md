# Backend Documentation Index

Last aligned: 2026-06-27  
Repo: `ivanjovicic/MathLearning`

This index defines which backend docs to read first, which are canonical, and which are evidence/status snapshots. Use it to save tokens and avoid treating stale notes as current architecture.

---

## Source-of-truth order

When docs disagree, use this order:

1. Current backend code and tests.
2. [`../AGENTS.md`](../AGENTS.md) — backend agent/contributor rules.
3. [`AGENT_QUICKSTART.md`](AGENT_QUICKSTART.md) — shortest safe path by task type.
4. [`BACKEND_REGRESSION_GUARDRAILS.md`](BACKEND_REGRESSION_GUARDRAILS.md) — historical bug classes and mandatory anti-regression prompt block.
5. [`ARCHITECTURE_OVERVIEW.md`](ARCHITECTURE_OVERVIEW.md) — repo/runtime architecture map.
6. [`API_ENDPOINT_INVENTORY.md`](API_ENDPOINT_INVENTORY.md) — current endpoint map.
7. [`backend_contract_gap_report.md`](backend_contract_gap_report.md) — backend/mobile contract evidence snapshot.
8. [`mobile_contract_idempotency_handoff.md`](mobile_contract_idempotency_handoff.md) — backend-side idempotency handoff.
9. [`BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md`](BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md) — current backend performance/optimization review and priority stack.
10. [`prompt_queues/backend_performance_optimization.md`](prompt_queues/backend_performance_optimization.md) — active backend performance prompt queue.
11. Cross-repo mobile docs in `ivanjovicic/Mathlearning-Mobile-App`.

If code/tests and docs disagree, inspect the implementation, then update docs in the same change.

---

## Canonical / working docs

| Document | Type | Use for | Notes |
|---|---|---|---|
| [`../AGENTS.md`](../AGENTS.md) | Agent rulebook | Rules for safe backend changes | Read first for every prompt. |
| [`AGENT_QUICKSTART.md`](AGENT_QUICKSTART.md) | Token-saving quickstart | Which files/tests to read for common tasks | Reduces rediscovery and context waste. |
| [`BACKEND_REGRESSION_GUARDRAILS.md`](BACKEND_REGRESSION_GUARDRAILS.md) | Regression guardrails | Historical bug classes, required prompt block, validation matrix | Mandatory for implementation prompts. |
| [`ARCHITECTURE_OVERVIEW.md`](ARCHITECTURE_OVERVIEW.md) | Architecture map | Project layout, startup, endpoint ownership, persistence, idempotency, background jobs | Update when runtime architecture changes. |
| [`API_ENDPOINT_INVENTORY.md`](API_ENDPOINT_INVENTORY.md) | Endpoint inventory | Current API route map and canonical/legacy split | Update whenever routes are added, removed, or changed. |
| [`BACKEND_CHANGE_CHECKLIST.md`](BACKEND_CHANGE_CHECKLIST.md) | Change checklist | Pre-commit safety gate for backend work | Use for code and docs changes. |
| [`COMMON_AGENT_PITFALLS.md`](COMMON_AGENT_PITFALLS.md) | Common mistakes | Avoid recurring errors in this repo | Use before implementing broad changes. |

---

## Regression / bug-prevention docs

| Document | Type | Use for | Notes |
|---|---|---|---|
| [`BACKEND_REGRESSION_GUARDRAILS.md`](BACKEND_REGRESSION_GUARDRAILS.md) | Mandatory guardrails | Prevent repeated bugs from commit history: migrations, auth scope, idempotency, contract shape, startup, query shape, UTF-8, warnings, admin Blazor, question authoring | Every implementation prompt must name the historical bug class it protects. |

---

## Performance / optimization docs

| Document | Type | Use for | Notes |
|---|---|---|---|
| [`BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md`](BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md) | Performance review | Backend hot-path findings, priority stack, and safe/not-safe optimization boundaries | Current performance planning snapshot. |
| [`prompt_queues/backend_performance_optimization.md`](prompt_queues/backend_performance_optimization.md) | Active prompt queue | Precise BE-PERF prompts for quiz/SRS/answer/leaderboard/startup/observability/route work | Use for future backend optimization work. |
| [`BACKEND_REVIEW_2026_06_27.md`](BACKEND_REVIEW_2026_06_27.md) | Safety review | Explanation endpoint safety/cache/rate-limit prompt input | Focused on explanations, not general performance. |

---

## Contract / evidence docs

| Document | Type | Use for | Notes |
|---|---|---|---|
| [`mobile_contract_idempotency_handoff.md`](mobile_contract_idempotency_handoff.md) | Backend-side handoff | Required behavior for retryable mobile mutations | Contract/handoff, not implementation proof. |
| [`backend_contract_gap_report.md`](backend_contract_gap_report.md) | Evidence/status snapshot | What backend currently implements, tests, and still risks | Update after implementation/test evidence changes. |

---

## Cross-repo mobile docs to consult

| Mobile document | Use for |
|---|---|
| `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_api_contract.md` | Canonical mobile/backend payload shapes. |
| `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_backend_contract_status.md` | Mobile-side backend parity matrix. |
| `ivanjovicic/Mathlearning-Mobile-App/docs/stabilization_status.md` | Mobile stabilization priorities and risk status. |
| `ivanjovicic/Mathlearning-Mobile-App/docs/prompt_queues/README.md` | Canonical mobile prompt queue router. |
| `ivanjovicic/Mathlearning-Mobile-App/docs/prompt_queues/backend_contracts.md` | Cross-repo/backend-contract prompt lane from the mobile repo. |

---

## Living code files that act as documentation

| File | Use for |
|---|---|
| `src/MathLearning.Api/Program.cs` | Startup, middleware, endpoint map order, health/metrics, Hangfire registration. |
| `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs` | DI registration, EF/Redis/Hangfire/OpenTelemetry/CORS/security setup. |
| `src/MathLearning.Api/Endpoints/*.cs` | Route definitions and HTTP boundary ownership. |
| `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs` | EF model ownership and DbSets. |
| `src/MathLearning.Infrastructure/Services/Leaderboard/DbBackedRedisLeaderboardService.cs` | DB fallback for Redis leaderboard and period/rank behavior. |
| `src/MathLearning.Infrastructure/Migrations/*` | Current schema and migration history. |
| `tests/MathLearning.Tests/Idempotency/*` | Idempotency behavior proof. |
| `tests/MathLearning.Tests/Contracts/*` | Mobile HTTP contract proof. |
| `tests/MathLearning.Tests/Endpoints/*` | Route/auth/scope proof. |

---

## Maintenance rules

Update this index when:

- a backend docs file is added or superseded
- a prompt queue is added or retired
- endpoint inventory changes
- idempotency or auth-scope policy changes
- new test groups become canonical evidence
- mobile contract changes affect backend runtime
- a performance hot path changes behavior, query shape, or budget
- a new repeated bug pattern appears in commit history

Do not duplicate long endpoint payload examples here. Link to the owning doc instead.

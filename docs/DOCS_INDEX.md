# Backend Documentation Index

Last aligned: 2026-07-01
Repo: `ivanjovicic/MathLearning`

This index defines which backend docs to read first, which are canonical, and which are evidence/status snapshots. Use it to save tokens and avoid treating stale notes as current architecture.

---

## Source-of-truth order

When docs disagree, use this order:

1. Current backend code and tests.
2. [`../AGENTS.md`](../AGENTS.md) — backend agent/contributor rules.
3. [`AGENT_SHARED_OPERATING_STANDARD.md`](AGENT_SHARED_OPERATING_STANDARD.md) — shared cross-repo agent rules for prompt shape, token budget, run evidence, score caps, mistake learning, validation honesty, and docs-only truth.
4. [`AGENT_RUN_LOG_ENFORCEMENT.md`](AGENT_RUN_LOG_ENFORCEMENT.md) — mandatory run-log and mistake-learning gate.
5. [`.ai/RUN_LOG_TEMPLATE.md`](../.ai/RUN_LOG_TEMPLATE.md) and [`.ai/runs/README.md`](../.ai/runs/README.md) — mandatory compact run-log format.
6. [`ai/learning/MISTAKE_LEDGER.md`](ai/learning/MISTAKE_LEDGER.md) — backend mistake memory.
7. [`AGENT_QUICKSTART.md`](AGENT_QUICKSTART.md) — shortest safe path by task type.
8. [`BACKEND_REGRESSION_GUARDRAILS.md`](BACKEND_REGRESSION_GUARDRAILS.md) — historical bug classes and mandatory anti-regression prompt block.
9. [`ARCHITECTURE_OVERVIEW.md`](ARCHITECTURE_OVERVIEW.md) — repo/runtime architecture map.
10. [`API_ENDPOINT_INVENTORY.md`](API_ENDPOINT_INVENTORY.md) — current endpoint map.
11. [`backend_contract_gap_report.md`](backend_contract_gap_report.md) — backend/mobile contract evidence snapshot.
12. [`mobile_contract_idempotency_handoff.md`](mobile_contract_idempotency_handoff.md) — backend-side idempotency handoff.
13. [`BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md`](BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md) — current backend performance/optimization review and priority stack.
14. [`prompt_queues/backend_performance_optimization.md`](prompt_queues/backend_performance_optimization.md) — backend performance prompt queue.
15. Cross-repo mobile docs in `ivanjovicic/Mathlearning-Mobile-App`.

If code/tests and docs disagree, inspect the implementation, then update docs in the same change.

---

## Canonical / working docs

| Document | Type | Use for | Notes |
|---|---|---|---|
| [`../AGENTS.md`](../AGENTS.md) | Agent rulebook | Rules for safe backend changes | Read first for every prompt. |
| [`AGENT_SHARED_OPERATING_STANDARD.md`](AGENT_SHARED_OPERATING_STANDARD.md) | Shared standard | Common cross-repo prompt/evidence/mistake-learning rules | Aligns backend with Flutter and AgentsWatch while preserving backend-specific validation. |
| [`AGENT_RUN_LOG_ENFORCEMENT.md`](AGENT_RUN_LOG_ENFORCEMENT.md) | Run-log gate | Mandatory `.ai/runs` evidence, score caps, mistake learning | Every non-trivial prompt. |
| [`AGENT_QUICKSTART.md`](AGENT_QUICKSTART.md) | Token-saving quickstart | Which files/tests to read for common tasks | Reduces rediscovery and context waste. |
| [`BACKEND_REGRESSION_GUARDRAILS.md`](BACKEND_REGRESSION_GUARDRAILS.md) | Regression guardrails | Historical bug classes, required prompt block, validation matrix | Mandatory for implementation prompts. |
| [`ARCHITECTURE_OVERVIEW.md`](ARCHITECTURE_OVERVIEW.md) | Architecture map | Project layout, startup, endpoint ownership, persistence, idempotency, background jobs | Update when runtime architecture changes. |
| [`API_ENDPOINT_INVENTORY.md`](API_ENDPOINT_INVENTORY.md) | Endpoint inventory | Current API route map and canonical/legacy split | Update whenever routes are added, removed, or changed. |
| [`BACKEND_CHANGE_CHECKLIST.md`](BACKEND_CHANGE_CHECKLIST.md) | Change checklist | Pre-commit safety gate for backend work | Use for code and docs changes. |
| [`COMMON_AGENT_PITFALLS.md`](COMMON_AGENT_PITFALLS.md) | Common mistakes | Avoid recurring errors in this repo | Use before implementing broad changes. |
| [`ai/learning/MISTAKE_LEDGER.md`](ai/learning/MISTAKE_LEDGER.md) | Mistake ledger | BACKEND-MISTAKE-* patterns and prevention | Read before start; update before Done. |

---

## Agent evidence / learning docs

| Document | Type | Use for | Notes |
|---|---|---|---|
| [`../.ai/RUN_LOG_TEMPLATE.md`](../.ai/RUN_LOG_TEMPLATE.md) | Run log template | Copy per prompt into `.ai/runs/` | Do not commit filled template as template. |
| [`../.ai/runs/README.md`](../.ai/runs/README.md) | Run log index | Naming, compactness, backend rules | One log per non-trivial prompt. |
| [`ai/learning/MISTAKE_CARD_TEMPLATE.md`](ai/learning/MISTAKE_CARD_TEMPLATE.md) | Mistake template | Add new BACKEND-MISTAKE-* cards | Cross-ref run logs. |
| [`ai/prompts/RUN_LOG_EVIDENCE_LINT_PROMPT.md`](ai/prompts/RUN_LOG_EVIDENCE_LINT_PROMPT.md) | Lint prompt | Fix misleading Done rows before runtime work | Docs-only. |
| [`ai/prompts/AGENT_MISTAKE_ROLLUP_PROMPT.md`](ai/prompts/AGENT_MISTAKE_ROLLUP_PROMPT.md) | Rollup prompt | Learn from last 5–8 run logs | Docs-only. |
| [`ai/prompts/BACKEND_EVIDENCE_BACKFILL_PROMPT.md`](ai/prompts/BACKEND_EVIDENCE_BACKFILL_PROMPT.md) | Backfill prompt | Repair missing logs for past commits | No runtime edits by default. |
| [`ai/prompts/CROSS_REPO_AGENT_STANDARD_SYNC_PROMPT.md`](ai/prompts/CROSS_REPO_AGENT_STANDARD_SYNC_PROMPT.md) | Cross-repo sync prompt | Keep backend, Flutter, and AgentsWatch agent rules aligned | Docs-only. |

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
| [`BACKEND_COLD_START_BUDGET.md`](BACKEND_COLD_START_BUDGET.md) | Cold-start budget | Blocking vs background startup phases, health evidence, staging smoke steps | BE-PERF-006 evidence. |
| [`BACKEND_REQUEST_PERFORMANCE_BUDGETS.md`](BACKEND_REQUEST_PERFORMANCE_BUDGETS.md) | Request-path budgets | p95 latency/query budgets per mobile flow, log/trace mapping, smoke steps | BE-PERF-007 evidence. |
| [`BACKEND_ROUTE_COMPATIBILITY_AUDIT.md`](BACKEND_ROUTE_COMPATIBILITY_AUDIT.md) | Route compatibility | Canonical vs legacy aliases, dual surfaces, duplicate-work matrix, follow-up prompts | BE-PERF-008 evidence. |
| [`prompt_queues/backend_performance_optimization.md`](prompt_queues/backend_performance_optimization.md) | Performance queue | BE-PERF prompts (001…008 complete/backfilled) | Performance lane. |
| [`prompt_queues/backend_critical_risk_prevention.md`](prompt_queues/backend_critical_risk_prevention.md) | Critical risk queue | BACKEND-CRIT-001…008 prompt-ready; audit-created, not Done | Security/settlement/idempotency lane. |
| [`prompt_queues/backend_second_pass_risk_prevention.md`](prompt_queues/backend_second_pass_risk_prevention.md) | Second-pass risk queue | BACKEND2-CRIT-001…008 prompt-ready; audit-created, not Done | Auth/proxy/jobs/authoring lane. |
| [`BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md`](BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md) | Static audit | Critical app-flow findings; creates CRIT prompts | Not fix proof. |
| [`BACKEND_SECOND_PASS_APP_FLOW_AUDIT_2026_07_01.md`](BACKEND_SECOND_PASS_APP_FLOW_AUDIT_2026_07_01.md) | Static audit | Second-pass findings; creates BACKEND2 prompts | Not fix proof. |
| [`BACKEND_CRITICAL_RISK_PREVENTION_RULES.md`](BACKEND_CRITICAL_RISK_PREVENTION_RULES.md) | Prevention rules | Risk classes for CRIT implementation prompts | Not fix proof. |
| [`BACKEND_SECOND_PASS_RISK_PREVENTION_RULES.md`](BACKEND_SECOND_PASS_RISK_PREVENTION_RULES.md) | Prevention rules | Risk classes for BACKEND2 prompts | Not fix proof. |
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
| `ivanjovicic/Mathlearning-Mobile-App/docs/AGENT_SHARED_OPERATING_STANDARD.md` | Shared agent rules from the Flutter side. |

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

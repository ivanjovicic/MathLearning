# Backend Documentation Index

Last aligned: 2026-07-16  
Repo: `ivanjovicic/MathLearning`

This index defines which backend docs to read first, which are canonical, and which are evidence/status snapshots. Use it to save tokens and avoid treating stale notes as current architecture.

---

## Source-of-truth order

When docs disagree, use this order:

1. Current backend code and tests.
2. [`../AGENTS.md`](../AGENTS.md) — backend agent/contributor rules.
3. [`AGENT_SHARED_OPERATING_STANDARD.md`](AGENT_SHARED_OPERATING_STANDARD.md) — shared cross-repo agent rules.
4. [`AGENT_RUN_LOG_ENFORCEMENT.md`](AGENT_RUN_LOG_ENFORCEMENT.md) — mandatory run-log and learning gate.
5. [`.ai/RUN_LOG_TEMPLATE.md`](../.ai/RUN_LOG_TEMPLATE.md) and [`.ai/runs/README.md`](../.ai/runs/README.md).
6. [`ai/learning/MISTAKE_LEDGER.md`](ai/learning/MISTAKE_LEDGER.md).
7. [`AGENT_QUICKSTART.md`](AGENT_QUICKSTART.md).
8. [`BACKEND_REGRESSION_GUARDRAILS.md`](BACKEND_REGRESSION_GUARDRAILS.md).
9. [`BACKEND_TEST_COVERAGE_STRATEGY.md`](BACKEND_TEST_COVERAGE_STRATEGY.md).
10. [`ARCHITECTURE_OVERVIEW.md`](ARCHITECTURE_OVERVIEW.md).
11. [`API_ENDPOINT_INVENTORY.md`](API_ENDPOINT_INVENTORY.md).
12. Current coverage/performance audits and active queues.
13. [`backend_contract_gap_report.md`](backend_contract_gap_report.md).
14. [`mobile_contract_idempotency_handoff.md`](mobile_contract_idempotency_handoff.md).
15. Cross-repo mobile docs in `ivanjovicic/Mathlearning-Mobile-App`.

If code/tests and docs disagree, inspect implementation and update docs in the same change.

---

## Canonical / working docs

| Document | Type | Use for | Notes |
|---|---|---|---|
| [`../AGENTS.md`](../AGENTS.md) | Agent rulebook | Safe backend changes | Read first. |
| [`AGENT_SHARED_OPERATING_STANDARD.md`](AGENT_SHARED_OPERATING_STANDARD.md) | Shared standard | Prompt/evidence/mistake-learning rules | Cross-repo aligned. |
| [`AGENT_RUN_LOG_ENFORCEMENT.md`](AGENT_RUN_LOG_ENFORCEMENT.md) | Run-log gate | Mandatory `.ai/runs` evidence and score caps | Every non-trivial prompt. |
| [`AGENT_QUICKSTART.md`](AGENT_QUICKSTART.md) | Quickstart | Minimal files/tests by task type | Reduces rediscovery. |
| [`BACKEND_REGRESSION_GUARDRAILS.md`](BACKEND_REGRESSION_GUARDRAILS.md) | Guardrails | Historical bug classes and validation matrix | Mandatory for implementation prompts. |
| [`BUGFIX_PATTERN_GUARDRAILS.md`](BUGFIX_PATTERN_GUARDRAILS.md) | Bugfix guardrails | Minimum regression evidence by bug class | Read before bug fixes. |
| [`BACKEND_TEST_COVERAGE_STRATEGY.md`](BACKEND_TEST_COVERAGE_STRATEGY.md) | Coverage strategy | Risk-first coverage layers and staged thresholds | Critical invariants before percentage. |
| [`ARCHITECTURE_OVERVIEW.md`](ARCHITECTURE_OVERVIEW.md) | Architecture map | Startup, projects, persistence, jobs | Update with runtime architecture. |
| [`API_ENDPOINT_INVENTORY.md`](API_ENDPOINT_INVENTORY.md) | Endpoint inventory | Current route/auth/canonical map | Updated through coverage pass 2. |
| [`BACKEND_CHANGE_CHECKLIST.md`](BACKEND_CHANGE_CHECKLIST.md) | Checklist | Pre-commit safety gate | Code and docs changes. |
| [`COMMON_AGENT_PITFALLS.md`](COMMON_AGENT_PITFALLS.md) | Mistakes | Common repo pitfalls | Read before broad work. |
| [`ai/learning/MISTAKE_LEDGER.md`](ai/learning/MISTAKE_LEDGER.md) | Mistake ledger | BACKEND-MISTAKE-* prevention | Read before start; update before Done. |

---

## Test coverage and quality

| Document | Type | Use for | Notes |
|---|---|---|---|
| [`BACKEND_TEST_COVERAGE_STRATEGY.md`](BACKEND_TEST_COVERAGE_STRATEGY.md) | Coverage strategy | Priorities, layers and CI thresholds | Percentage is secondary to critical branch proof. |
| [`BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`](BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md) | Coverage audit pass 1 | Validated vs implemented vs prompt-ready matrix | Includes ingest, bug/maintenance auth and CI coverage visibility. |
| [`BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md`](BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md) | Coverage audit pass 2 | Maintenance read-only, analytics/explanation contracts, pagination and test-auth | Adds 38 executable cases; execution still required. |
| [`prompt_queues/backend_test_coverage.md`](prompt_queues/backend_test_coverage.md) | Primary test queue | Active BACKEND-TEST and API/DB packages | Rows remain Needs validation without executable evidence. |
| [`prompt_queues/backend_test_followups_2026_07_03.md`](prompt_queues/backend_test_followups_2026_07_03.md) | Follow-up queue pass 1 | Durable ingest, outbox, PostgreSQL and operational risks | Detailed prompts 022–035. |
| [`prompt_queues/backend_test_followups_pass2_2026_07_03.md`](prompt_queues/backend_test_followups_pass2_2026_07_03.md) | Follow-up queue pass 2 | Maintenance/explanation/paging/policy prompts | Detailed prompts 042–047. |
| [`prompt_queues/backend_latest_commit_followups_2026_07_11.md`](prompt_queues/backend_latest_commit_followups_2026_07_11.md) | Latest-commit closure queue | Validate recent implementation, bind CI artifacts, lint evidence and reconcile prompt ownership | Run before another broad backend audit. |
| [`prompt_queues/backend_api_db_residuals_2026_07_11.md`](prompt_queues/backend_api_db_residuals_2026_07_11.md) | API/DB residual queue pass 1 | Answer disclosure, quiz authority, progress/sync trust, offline bundles, token storage and user reads | Detailed prompts BACKEND-API-DB-001…008. |
| [`prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md`](prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md) | API/DB residual queue pass 2 | Cosmetics/economy entitlement, leaderboard identity/parity, registration, photo avatars and pending recovery | Detailed prompts BACKEND-API-DB-009…015. |
| [`prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`](prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md) | API/DB residual queue pass 3 | Private bug screenshots, credential abuse protection, revocable access sessions and cosmetics catalog readiness | Detailed prompts BACKEND-API-DB-016…019; Admin project excluded. |
| [`prompt_queues/backend_failing_test_followups_2026_07_11.md`](prompt_queues/backend_failing_test_followups_2026_07_11.md) | Migration follow-up queue | Remaining clean/upgraded PostgreSQL cosmetics migration blocker | Canonical `BACKEND-MIGRATION-001`; do not duplicate. |
| [`prompt_queues/BACKEND-TEST-048-index-bloat-validation.md`](prompt_queues/BACKEND-TEST-048-index-bloat-validation.md) | PostgreSQL test prompt | Prove or replace index-bloat metric | Static formula concern requires provider evidence. |
| [`../tests/MathLearning.Tests/coverage.runsettings`](../tests/MathLearning.Tests/coverage.runsettings) | Coverage settings | Cobertura/JSON collection | Used by database-validation CI. |
| [`../.github/workflows/database-validation.yml`](../.github/workflows/database-validation.yml) | CI workflow | Build, PostgreSQL schema, full tests, coverage summary and startup smoke | Exact successful head/artifact evidence still required. |

---

## Performance / optimization and bug-risk docs

| Document | Type | Use for | Notes |
|---|---|---|---|
| [`BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md`](BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md) | Performance review | Original hot paths and safe optimization boundaries | Covers BE-PERF-001…008. |
| [`BACKEND_PERFORMANCE_BUG_AUDIT_2026_07_03.md`](BACKEND_PERFORMANCE_BUG_AUDIT_2026_07_03.md) | Static audit | Adaptive, weakness, XP reset, rate-limit, read-side mutation, cache, outbox and observability risks | Not runtime-fix proof. |
| [`BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11.md`](BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11.md) | Static API/DB audit pass 1 | Mobile response leakage, session/progress/sync authority, offline versioning, token storage and user queries | Admin excluded; not runtime-fix proof. |
| [`BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11_PASS2.md`](BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11_PASS2.md) | Static API/DB audit pass 2 | Remaining economy/cosmetics, leaderboard, registration, avatar and pending-operation risks | Admin excluded; not runtime-fix proof. |
| [`BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_16_PASS3.md`](BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_16_PASS3.md) | Static API/DB audit pass 3 | Private screenshot storage, credential/account protection, access-session revocation and versioned catalog readiness | Admin excluded; creates BACKEND-API-DB-016…019. |
| [`prompt_queues/backend_performance_optimization.md`](prompt_queues/backend_performance_optimization.md) | Performance queue pass 1 | Quiz, SRS, replay, leaderboard, Redis, startup, budgets and route bloat | BE-PERF-001…008. |
| [`prompt_queues/backend_performance_followups_2026_07_03.md`](prompt_queues/backend_performance_followups_2026_07_03.md) | Performance/bug queue pass 2 | Atomic mutations, bounded workers/state, pure reads, cache/outbox and observability | BE-PERF-009…017. |
| [`BACKEND_COLD_START_BUDGET.md`](BACKEND_COLD_START_BUDGET.md) | Cold-start budget | Blocking vs background startup | BE-PERF-006 evidence. |
| [`BACKEND_REQUEST_PERFORMANCE_BUDGETS.md`](BACKEND_REQUEST_PERFORMANCE_BUDGETS.md) | Request budgets | p95/query budgets | BE-PERF-007 evidence. |
| [`BACKEND_ROUTE_COMPATIBILITY_AUDIT.md`](BACKEND_ROUTE_COMPATIBILITY_AUDIT.md) | Route audit | Canonical vs legacy and duplicate-work risk | BE-PERF-008 evidence. |
| [`prompt_queues/backend_critical_risk_prevention.md`](prompt_queues/backend_critical_risk_prevention.md) | Critical risk queue | BACKEND-CRIT prompts | Security/settlement/idempotency lane. |
| [`prompt_queues/backend_second_pass_risk_prevention.md`](prompt_queues/backend_second_pass_risk_prevention.md) | Second-pass risk queue | BACKEND2 prompts | Auth/proxy/jobs/authoring lane. |
| [`BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md`](BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md) | Static audit | Critical app-flow findings | Not fix proof. |
| [`BACKEND_SECOND_PASS_APP_FLOW_AUDIT_2026_07_01.md`](BACKEND_SECOND_PASS_APP_FLOW_AUDIT_2026_07_01.md) | Static audit | Second-pass findings | Not fix proof. |
| [`BACKEND_REVIEW_2026_06_27.md`](BACKEND_REVIEW_2026_06_27.md) | Safety review | Explanation safety/cache/rate-limit inputs | Focused review. |

---

## Agent evidence / learning docs

| Document | Type | Use for | Notes |
|---|---|---|---|
| [`../.ai/RUN_LOG_TEMPLATE.md`](../.ai/RUN_LOG_TEMPLATE.md) | Run log template | Copy per prompt | Do not overwrite template. |
| [`../.ai/runs/README.md`](../.ai/runs/README.md) | Run log index | Naming and compact evidence rules | One log per non-trivial prompt. |
| [`ai/learning/MISTAKE_CARD_TEMPLATE.md`](ai/learning/MISTAKE_CARD_TEMPLATE.md) | Mistake template | Add BACKEND-MISTAKE cards | Cross-reference run logs. |
| [`ai/prompts/RUN_LOG_EVIDENCE_LINT_PROMPT.md`](ai/prompts/RUN_LOG_EVIDENCE_LINT_PROMPT.md) | Lint prompt | Repair misleading Done rows | Docs-only. |
| [`ai/prompts/AGENT_MISTAKE_ROLLUP_PROMPT.md`](ai/prompts/AGENT_MISTAKE_ROLLUP_PROMPT.md) | Rollup prompt | Learn from recent runs | Docs-only. |
| [`ai/prompts/BACKEND_EVIDENCE_BACKFILL_PROMPT.md`](ai/prompts/BACKEND_EVIDENCE_BACKFILL_PROMPT.md) | Backfill prompt | Repair missing evidence | No runtime edits by default. |
| [`ai/prompts/CROSS_REPO_AGENT_STANDARD_SYNC_PROMPT.md`](ai/prompts/CROSS_REPO_AGENT_STANDARD_SYNC_PROMPT.md) | Sync prompt | Align backend/mobile/AgentsWatch rules | Docs-only. |

---

## Contract / evidence docs

| Document | Type | Use for | Notes |
|---|---|---|---|
| [`mobile_contract_idempotency_handoff.md`](mobile_contract_idempotency_handoff.md) | Handoff | Retryable mobile mutation behavior | Contract, not implementation proof. |
| [`backend_contract_gap_report.md`](backend_contract_gap_report.md) | Status snapshot | Current backend/mobile evidence and risks | Update after contract changes. |

---

## Cross-repo mobile docs to consult

| Mobile document | Use for |
|---|---|
| `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_api_contract.md` | Canonical payloads. |
| `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_backend_contract_status.md` | Mobile/backend parity. |
| `ivanjovicic/Mathlearning-Mobile-App/docs/stabilization_status.md` | Mobile stabilization priorities. |
| `ivanjovicic/Mathlearning-Mobile-App/docs/prompt_queues/backend_contracts.md` | Cross-repo contract prompts. |
| `ivanjovicic/Mathlearning-Mobile-App/docs/AGENT_SHARED_OPERATING_STANDARD.md` | Shared agent rules. |

---

## Living code files that act as documentation

| File | Use for |
|---|---|
| `src/MathLearning.Api/Program.cs` | Startup, middleware, endpoint mapping, health/metrics and Hangfire. |
| `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs` | API DI, DB/Redis/Hangfire/security setup. |
| `src/MathLearning.Infrastructure/DependencyInjection.cs` | Infrastructure DI including shared maintenance service. |
| `src/MathLearning.Api/Endpoints/*.cs` | HTTP route ownership and policies. |
# Backend Documentation Index

Last aligned: 2026-07-16  
Repo: `ivanjovicic/MathLearning`

This index defines which backend docs to read first, which are canonical and which are evidence/status snapshots. Use it to avoid stale-note authority and broad rediscovery.

## Source-of-truth order

1. Current backend code, focused tests and executable tooling.
2. [`.ai/SOURCE_OF_TRUTH.md`](../.ai/SOURCE_OF_TRUTH.md) for agent-document ownership.
3. [`../AGENTS.md`](../AGENTS.md) for backend engineering invariants.
4. The canonical owner named by the source-of-truth map.
5. Short entrypoints/indexes.
6. Current queues and run evidence.
7. Dated audits/plans/history.

If code/tests and durable docs disagree, inspect implementation and update the owning doc in the same change.

## Minimal agent entry path

| Document | Use for |
|---|---|
| [`.ai/README.md`](../.ai/README.md) | Low-context start, repository-root proof and bounded packet. |
| [`../AGENTS.md`](../AGENTS.md) | Backend architecture, auth, idempotency, migration and delivery invariants. |
| [`.ai/SOURCE_OF_TRUTH.md`](../.ai/SOURCE_OF_TRUTH.md) | Resolve agent-rule ownership. |
| [`.ai/TOKEN_BUDGETS.md`](../.ai/TOKEN_BUDGETS.md) | Time/context/read/search/edit limits. |
| [`.ai/VALIDATION_SELECTOR.md`](../.ai/VALIDATION_SELECTOR.md) | Focused .NET/PostgreSQL/tooling/docs/CI proof. |
| [`.ai/PROMPT_LINT_CHECKLIST.md`](../.ai/PROMPT_LINT_CHECKLIST.md) | Prompt contract v2 and admission v3. |
| [`AGENT_COMMAND_PLAYBOOK.md`](AGENT_COMMAND_PLAYBOOK.md) | Guarded commands/timeouts and command evidence. |
| [`prompt_queues/README.md`](prompt_queues/README.md) | Canonical queue priority/collision router. |
| [`prompt_queues/PROMPT_LIFECYCLE.md`](prompt_queues/PROMPT_LIFECYCLE.md) | Queue states, assignment, delivery and archive. |

Open only the owner needed for the next decision.

## Canonical engineering and agent docs

| Document | Type | Use for |
|---|---|---|
| [`AGENT_SHARED_OPERATING_STANDARD.md`](AGENT_SHARED_OPERATING_STANDARD.md) | shared standard | Cross-repo minimum, evidence and score caps. |
| [`AGENT_RUN_LOG_ENFORCEMENT.md`](AGENT_RUN_LOG_ENFORCEMENT.md) | evidence gate | Run-log fields and honest completion. |
| [`.ai/RUN_LOG_TEMPLATE.md`](../.ai/RUN_LOG_TEMPLATE.md) | template | Copy per non-trivial prompt. |
| [`.ai/runs/README.md`](../.ai/runs/README.md) | evidence index | Naming and backend evidence rules. |
| [`AGENT_QUICKSTART.md`](AGENT_QUICKSTART.md) | quickstart | Minimal files/tests by task type. |
| [`BACKEND_REGRESSION_GUARDRAILS.md`](BACKEND_REGRESSION_GUARDRAILS.md) | guardrails | High-risk invariants/validation matrix. |
| [`BUGFIX_PATTERN_GUARDRAILS.md`](BUGFIX_PATTERN_GUARDRAILS.md) | bugfix gate | Regression proof by bug class. |
| [`BACKEND_TEST_COVERAGE_STRATEGY.md`](BACKEND_TEST_COVERAGE_STRATEGY.md) | strategy | Risk-first test layers. |
| [`BACKEND_CHANGE_CHECKLIST.md`](BACKEND_CHANGE_CHECKLIST.md) | checklist | Pre-delivery code/docs safety. |
| [`COMMON_AGENT_PITFALLS.md`](COMMON_AGENT_PITFALLS.md) | pitfalls | Recurring repository mistakes. |
| [`ai/learning/MISTAKE_LEDGER.md`](ai/learning/MISTAKE_LEDGER.md) | learning owner | `BACKEND-MISTAKE-*` prevention. |
| [`ai/TASK_TEMPLATE.md`](ai/TASK_TEMPLATE.md) | prompt template | New backend v2/v3 prompt shape. |

## Architecture and contracts

| Document | Use for |
|---|---|
| [`ARCHITECTURE_OVERVIEW.md`](ARCHITECTURE_OVERVIEW.md) | Startup, projects, persistence and jobs. |
| [`API_ENDPOINT_INVENTORY.md`](API_ENDPOINT_INVENTORY.md) | Current route/auth/canonical endpoint map. |
| [`backend_contract_gap_report.md`](backend_contract_gap_report.md) | Backend/mobile evidence and contract gaps. |
| [`mobile_contract_idempotency_handoff.md`](mobile_contract_idempotency_handoff.md) | Retryable mobile mutation/idempotency handoff. |
| [`BACKEND_COLD_START_BUDGET.md`](BACKEND_COLD_START_BUDGET.md) | Blocking vs background startup budget. |
| [`BACKEND_REQUEST_PERFORMANCE_BUDGETS.md`](BACKEND_REQUEST_PERFORMANCE_BUDGETS.md) | Request/query budgets. |
| [`BACKEND_ROUTE_COMPATIBILITY_AUDIT.md`](BACKEND_ROUTE_COMPATIBILITY_AUDIT.md) | Canonical vs legacy route ownership. |

## Current queues

Use [`prompt_queues/README.md`](prompt_queues/README.md) for selection order.

| Queue | Use for |
|---|---|
| [`prompt_queues/backend_critical_risk_prevention.md`](prompt_queues/backend_critical_risk_prevention.md) | Security/settlement/idempotency critical lane. |
| [`prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`](prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md) | Current API/DB security/readiness residuals. |
| [`prompt_queues/backend_failing_test_followups_2026_07_11.md`](prompt_queues/backend_failing_test_followups_2026_07_11.md) | Known migration/test blockers. |
| [`prompt_queues/backend_latest_commit_followups_2026_07_11.md`](prompt_queues/backend_latest_commit_followups_2026_07_11.md) | Recent implementation validation/CI/evidence closure. |
| [`prompt_queues/backend_test_coverage.md`](prompt_queues/backend_test_coverage.md) | Primary test/provider queue. |
| [`prompt_queues/backend_performance_followups_2026_07_03.md`](prompt_queues/backend_performance_followups_2026_07_03.md) | Atomicity, bounded work, cache/outbox and observability. |
| [`prompt_queues/backend_second_pass_risk_prevention.md`](prompt_queues/backend_second_pass_risk_prevention.md) | Secondary auth/proxy/jobs/authoring lane. |

Earlier pass queues remain historical/supporting evidence unless the current router selects them.

## Audits and snapshots

These inform prompts but do not prove runtime fixes:

- [`BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`](BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md)
- [`BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md`](BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md)
- [`BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md`](BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md)
- [`BACKEND_PERFORMANCE_BUG_AUDIT_2026_07_03.md`](BACKEND_PERFORMANCE_BUG_AUDIT_2026_07_03.md)
- [`BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11.md`](BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11.md)
- [`BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11_PASS2.md`](BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11_PASS2.md)
- [`BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_16_PASS3.md`](BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_16_PASS3.md)
- [`BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md`](BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md)
- [`BACKEND_SECOND_PASS_APP_FLOW_AUDIT_2026_07_01.md`](BACKEND_SECOND_PASS_APP_FLOW_AUDIT_2026_07_01.md)

## Agent tooling and workflows

| File | Use for |
|---|---|
| [`../scripts/run_guarded.py`](../scripts/run_guarded.py) | Wall/idle timeout and process-tree termination. |
| [`../scripts/validate_agent_prompt.py`](../scripts/validate_agent_prompt.py) | Forward-only v2/v3 prompt lint. |
| [`../scripts/validate_agent_evidence.py`](../scripts/validate_agent_evidence.py) | Queue Done/run-log evidence consistency. |
| [`../scripts/validate_agent_system.py`](../scripts/validate_agent_system.py) | Agent-doc links, required wiring and Flutter-command leak prevention. |
| [`../.github/workflows/agent-system-validation.yml`](../.github/workflows/agent-system-validation.yml) | Automatic Python agent-system tests/checks. |
| [`../.github/workflows/agent-evidence-validation.yml`](../.github/workflows/agent-evidence-validation.yml) | Manual evidence audit. |
| [`../.github/workflows/database-validation.yml`](../.github/workflows/database-validation.yml) | Build, PostgreSQL schema, tests, coverage and startup smoke. |

## Cross-repo mobile docs

Consult as needed:

- `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_api_contract.md`
- `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_backend_contract_status.md`
- `ivanjovicic/Mathlearning-Mobile-App/docs/stabilization_status.md`
- `ivanjovicic/Mathlearning-Mobile-App/docs/prompt_queues/backend_contracts.md`

## Living code documentation

- `src/MathLearning.Api/Program.cs` — startup, middleware, endpoint mapping, health/metrics and Hangfire.
- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs` — API DI, DB/Redis/Hangfire/security setup.
- `src/MathLearning.Infrastructure/DependencyInjection.cs` — infrastructure DI/services.
- `src/MathLearning.Api/Endpoints/*.cs` — route ownership/policies.

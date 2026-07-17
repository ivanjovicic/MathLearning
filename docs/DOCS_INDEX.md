# Backend Documentation Index

Last aligned: 2026-07-17  
Owner: `backend-docs-system`

Use the shortest path that answers the next decision. Registered durable owners are listed in generated [`DOCS_REGISTRY.md`](DOCS_REGISTRY.md); transient queues, prompts, audits and run logs are not current architecture authority.

## 60-second agent path

| Resource | Use |
|---|---|
| [`.ai/README.md`](../.ai/README.md) | Fast start, user-assigned bypass and minimal packet |
| [`../AGENTS.md`](../AGENTS.md) | Backend invariants |
| [`DOCUMENTATION_SYSTEM.md`](DOCUMENTATION_SYSTEM.md) | Durable/transient rules and context routing |
| [`.ai/TOKEN_BUDGETS.md`](../.ai/TOKEN_BUDGETS.md) | Micro/low/medium/high limits |
| [`.ai/VALIDATION_SELECTOR.md`](../.ai/VALIDATION_SELECTOR.md) | Narrow proof selection |

Do not chain-read this table. For source-path-specific docs run:

```powershell
python scripts/check_documentation_health.py --context <path>
```

## Canonical workflow owners

| Resource | Owns |
|---|---|
| [`.ai/SOURCE_OF_TRUTH.md`](../.ai/SOURCE_OF_TRUTH.md) | Workflow-document authority |
| [`.ai/PROMPT_LINT_CHECKLIST.md`](../.ai/PROMPT_LINT_CHECKLIST.md) | Formal prompt v2/admission v3 |
| [`AGENT_COMMAND_PLAYBOOK.md`](AGENT_COMMAND_PLAYBOOK.md) | Guarded commands/timeouts |
| [`AGENT_RUN_LOG_ENFORCEMENT.md`](AGENT_RUN_LOG_ENFORCEMENT.md) | Evidence caps and self-SHA |
| [`prompt_queues/README.md`](prompt_queues/README.md) | Current queue priority/archive router |
| [`prompt_queues/PROMPT_LIFECYCLE.md`](prompt_queues/PROMPT_LIFECYCLE.md) | Queue ownership/delivery/closure |

## Engineering and contract owners

| Resource | Use |
|---|---|
| [`ARCHITECTURE_OVERVIEW.md`](ARCHITECTURE_OVERVIEW.md) | Startup, projects, persistence and jobs |
| [`API_ENDPOINT_INVENTORY.md`](API_ENDPOINT_INVENTORY.md) | Route/auth/compatibility inventory |
| [`mobile_api_contract.md`](mobile_api_contract.md) | Mobile request/response contract |
| [`mobile_contract_idempotency_handoff.md`](mobile_contract_idempotency_handoff.md) | Retryable mutation operation identity |
| [`REFRESH_TOKEN_SYSTEM.md`](REFRESH_TOKEN_SYSTEM.md) | Refresh-token and access-token invalidation |
| [`BUGFIX_PATTERN_GUARDRAILS.md`](BUGFIX_PATTERN_GUARDRAILS.md) | Regression proof by bug class |
| [`BACKEND_REGRESSION_GUARDRAILS.md`](BACKEND_REGRESSION_GUARDRAILS.md) | High-risk invariants |
| [`BACKEND_TEST_COVERAGE_STRATEGY.md`](BACKEND_TEST_COVERAGE_STRATEGY.md) | Risk-first validation layers |
| [`BACKEND_COLD_START_BUDGET.md`](BACKEND_COLD_START_BUDGET.md) | Startup/readiness budget |
| [`BACKEND_REQUEST_PERFORMANCE_BUDGETS.md`](BACKEND_REQUEST_PERFORMANCE_BUDGETS.md) | Request/query budgets |

## Current transient route

Use the registered queue router. The current-main residual queue is `docs/prompt_queues/backend_cross_repo_current_main_2026_07_17.md`. Completed items are preserved in dated archives; stale old Ready text never overrides an archive or main-verified evidence.

## Health commands

```powershell
python -m unittest -v scripts/test_check_documentation_health.py
python scripts/check_documentation_health.py --full-links
python scripts/check_documentation_health.py --write-registry
```

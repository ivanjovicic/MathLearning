# Backend Documentation Registry

Generated from [`DOCS_MANIFEST.json`](DOCS_MANIFEST.json). Do not edit by hand.

| Document | Class | Owner | Impact | Last verified | Purpose |
|---|---|---|---|---|---|
| [`.ai/PROMPT_LINT_CHECKLIST.md`](../.ai/PROMPT_LINT_CHECKLIST.md) | prompt-policy | `backend-agent-system` | required | 2026-07-17 | Forward-only prompt contract and admission requirements. |
| [`.ai/README.md`](../.ai/README.md) | agent-entrypoint | `backend-agent-system` | required | 2026-07-17 | Fast repository-aware task start and minimal read packet. |
| [`.ai/RUN_LOG_TEMPLATE.md`](../.ai/RUN_LOG_TEMPLATE.md) | evidence-template | `backend-agent-system` | required | 2026-07-17 | Compact evidence v2 shape. |
| [`.ai/SOURCE_OF_TRUTH.md`](../.ai/SOURCE_OF_TRUTH.md) | ownership-map | `backend-agent-system` | required | 2026-07-17 | Canonical ownership for agent and documentation rules. |
| [`.ai/TOKEN_BUDGETS.md`](../.ai/TOKEN_BUDGETS.md) | agent-policy | `backend-agent-system` | required | 2026-07-17 | Bound reads, searches, edits and execution time. |
| [`.ai/VALIDATION_SELECTOR.md`](../.ai/VALIDATION_SELECTOR.md) | validation-policy | `backend-agent-system` | required | 2026-07-17 | Select the narrowest sufficient executable proof. |
| [`AGENTS.md`](../AGENTS.md) | agent-rulebook | `backend-agent-system` | advisory | 2026-07-17 | Backend engineering invariants and agent completion rules. |
| [`docs/AGENT_COMMAND_PLAYBOOK.md`](AGENT_COMMAND_PLAYBOOK.md) | command-policy | `backend-agent-system` | required | 2026-07-16 | Guarded command shape, timeouts and shell safety. |
| [`docs/AGENT_RUN_LOG_ENFORCEMENT.md`](AGENT_RUN_LOG_ENFORCEMENT.md) | evidence-policy | `backend-agent-system` | required | 2026-07-17 | Evidence validation, completion caps and self-SHA rules. |
| [`docs/API_ENDPOINT_INVENTORY.md`](API_ENDPOINT_INVENTORY.md) | api-contract | `backend-api-contract` | required | 2026-07-16 | Route, authorization and compatibility ownership. |
| [`docs/ARCHITECTURE_OVERVIEW.md`](ARCHITECTURE_OVERVIEW.md) | architecture | `backend-architecture` | advisory | 2026-07-16 | Current startup, projects, persistence and jobs. |
| [`docs/BACKEND_COLD_START_BUDGET.md`](BACKEND_COLD_START_BUDGET.md) | performance-contract | `backend-performance` | advisory | 2026-07-16 | Blocking startup and readiness budget. |
| [`docs/BACKEND_REGRESSION_GUARDRAILS.md`](BACKEND_REGRESSION_GUARDRAILS.md) | engineering-guardrail | `backend-quality` | advisory | 2026-07-16 | High-risk auth, settlement, persistence and provider invariants. |
| [`docs/BACKEND_REQUEST_PERFORMANCE_BUDGETS.md`](BACKEND_REQUEST_PERFORMANCE_BUDGETS.md) | performance-contract | `backend-performance` | advisory | 2026-07-03 | Request, query and allocation budgets. |
| [`docs/BACKEND_TEST_COVERAGE_STRATEGY.md`](BACKEND_TEST_COVERAGE_STRATEGY.md) | validation-strategy | `backend-quality` | advisory | 2026-07-11 | Risk-first test layers and provider evidence. |
| [`docs/BUGFIX_PATTERN_GUARDRAILS.md`](BUGFIX_PATTERN_GUARDRAILS.md) | engineering-guardrail | `backend-quality` | advisory | 2026-07-16 | Regression proof expectations by bug class. |
| [`docs/DOCS_INDEX.md`](DOCS_INDEX.md) | documentation-index | `backend-docs-system` | required | 2026-07-17 | Shortest route to registered durable owners. |
| [`docs/DOCS_REGISTRY.md`](DOCS_REGISTRY.md) | generated-registry | `backend-docs-system` | required | 2026-07-17 | Generated durable document inventory. |
| [`docs/DOCUMENTATION_SYSTEM.md`](DOCUMENTATION_SYSTEM.md) | documentation-policy | `backend-docs-system` | required | 2026-07-17 | Durable/transient ownership, manifest, registry and health rules. |
| [`docs/mobile_api_contract.md`](mobile_api_contract.md) | cross-repo-contract | `backend-mobile-contract` | required | 2026-07-16 | Mobile request, response, retry and compatibility contract. |
| [`docs/mobile_contract_idempotency_handoff.md`](mobile_contract_idempotency_handoff.md) | cross-repo-contract | `backend-mobile-contract` | required | 2026-07-03 | Retryable mobile mutation operation identity and settlement handoff. |
| [`docs/prompt_queues/PROMPT_LIFECYCLE.md`](prompt_queues/PROMPT_LIFECYCLE.md) | queue-policy | `backend-agent-system` | required | 2026-07-17 | Queue states, ownership, delivery, archive and cross-repo handoff. |
| [`docs/prompt_queues/README.md`](prompt_queues/README.md) | queue-router | `backend-agent-system` | required | 2026-07-17 | Current active order, archive and collision routing. |
| [`docs/REFRESH_TOKEN_SYSTEM.md`](REFRESH_TOKEN_SYSTEM.md) | security-contract | `backend-auth` | required | 2026-07-16 | Refresh-token rotation and access-token invalidation behavior. |

# Backend Documentation Index

Last aligned: 2026-07-17  
Repo: `ivanjovicic/MathLearning`

Use the shortest path that answers the next decision. Current code/tests/tooling override docs; dated audits/logs are evidence, not authority.

## 60-second agent path

| Resource | Use |
|---|---|
| [`.ai/README.md`](../.ai/README.md) | Fast start, user-assigned bypass and minimal packet |
| [`../AGENTS.md`](../AGENTS.md) | Backend invariants |
| [`../scripts/agent_run.py`](../scripts/agent_run.py) | Plan/start/finish compact evidence |
| [`ai/learning/MISTAKE_INDEX.json`](ai/learning/MISTAKE_INDEX.json) | Route task area to relevant mistakes/docs/proof |
| [`.ai/TOKEN_BUDGETS.md`](../.ai/TOKEN_BUDGETS.md) | Micro/low/medium/high limits |
| [`.ai/VALIDATION_SELECTOR.md`](../.ai/VALIDATION_SELECTOR.md) | Narrow proof selection |

Do not chain-read this table.

## Canonical workflow owners

| Resource | Owns |
|---|---|
| [`.ai/SOURCE_OF_TRUTH.md`](../.ai/SOURCE_OF_TRUTH.md) | Workflow-document authority |
| [`AGENT_COMMAND_PLAYBOOK.md`](AGENT_COMMAND_PLAYBOOK.md) | Guarded command shape/timeouts |
| [`.ai/PROMPT_LINT_CHECKLIST.md`](../.ai/PROMPT_LINT_CHECKLIST.md) | Formal prompt v2/admission v3 |
| [`prompt_queues/README.md`](prompt_queues/README.md) | Queue priority/router |
| [`prompt_queues/PROMPT_LIFECYCLE.md`](prompt_queues/PROMPT_LIFECYCLE.md) | Queue states/claim/closure |
| [`.ai/RUN_LOG_TEMPLATE.md`](../.ai/RUN_LOG_TEMPLATE.md) | Compact evidence v2 |
| [`AGENT_RUN_LOG_ENFORCEMENT.md`](AGENT_RUN_LOG_ENFORCEMENT.md) | Score caps/changed validation/self SHA |
| [`ai/learning/MISTAKE_LEDGER.md`](ai/learning/MISTAKE_LEDGER.md) | Detailed mistake cards |

## Agent tooling and speed measurement

| File | Use |
|---|---|
| [`../scripts/agent_run.py`](../scripts/agent_run.py) | Removes manual plan/log boilerplate and records timing |
| [`../scripts/validate_agent_evidence.py`](../scripts/validate_agent_evidence.py) | Changed-line/changed-log evidence validation |
| [`../scripts/analyze_agent_runs.py`](../scripts/analyze_agent_runs.py) | Unknown timing, mixed lanes, oversized logs and waste summary |
| [`../scripts/validate_agent_system.py`](../scripts/validate_agent_system.py) | Wiring/link/slow-default/unknown-mistake checks |
| [`../scripts/ci/classify_backend_changes.py`](../scripts/ci/classify_backend_changes.py) | Decide whether expensive .NET/PostgreSQL CI is required |
| [`../.github/workflows/agent-system-validation.yml`](../.github/workflows/agent-system-validation.yml) | Fast agent-system checks |
| [`../.github/workflows/database-validation.yml`](../.github/workflows/database-validation.yml) | Changed-scope gate + full runtime DB suite |

Latest throughput analysis: [`ai/learning/AGENT_SPEED_AUDIT_2026_07_17.md`](ai/learning/AGENT_SPEED_AUDIT_2026_07_17.md).

## Engineering owners

| Document | Use |
|---|---|
| [`ARCHITECTURE_OVERVIEW.md`](ARCHITECTURE_OVERVIEW.md) | Startup/projects/persistence/jobs |
| [`API_ENDPOINT_INVENTORY.md`](API_ENDPOINT_INVENTORY.md) | Route/auth/contract ownership |
| [`BUGFIX_PATTERN_GUARDRAILS.md`](BUGFIX_PATTERN_GUARDRAILS.md) | Regression proof by bug class |
| [`BACKEND_REGRESSION_GUARDRAILS.md`](BACKEND_REGRESSION_GUARDRAILS.md) | High-risk invariants |
| [`BACKEND_TEST_COVERAGE_STRATEGY.md`](BACKEND_TEST_COVERAGE_STRATEGY.md) | Risk-first test layers |
| [`mobile_contract_idempotency_handoff.md`](mobile_contract_idempotency_handoff.md) | Mobile retry/idempotency contract |
| [`BACKEND_COLD_START_BUDGET.md`](BACKEND_COLD_START_BUDGET.md) | Startup budget |
| [`BACKEND_REQUEST_PERFORMANCE_BUDGETS.md`](BACKEND_REQUEST_PERFORMANCE_BUDGETS.md) | Request/query budgets |

## Current queue route

Use [`prompt_queues/README.md`](prompt_queues/README.md). Current priority begins with critical risk, current API/DB residuals, proven failing tests, latest-delivery closure, test/provider packages, then performance/secondary lanes.

A user-assigned bounded task bypasses queue discovery.

## Dated audits/snapshots

Coverage, performance, critical-flow and API/DB audits inform bounded prompts but never prove runtime fixes. Open a dated audit only when its finding is the current owner/evidence source; do not read all passes.

## Cross-repo docs

Consult Flutter contract/status docs only when a public backend/mobile boundary is touched. Record touched/deferred paths in compact evidence.

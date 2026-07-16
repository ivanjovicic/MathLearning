# Backend AI Workflow Source-of-Truth Map

Last aligned: 2026-07-16  
Owner: `backend-agent-system`

Use this map when an agent rule, command, prompt contract, validation requirement or queue status appears in more than one document. Short entrypoints may summarize an owner but must not redefine it.

## Authority order

When sources disagree:

1. current backend code, focused tests and executable tooling;
2. this owner map;
3. the canonical owner named below;
4. short summaries and links in entrypoint/index documents;
5. dated audits, queue snapshots, evidence logs and historical notes.

Chat memory and old queue rows never override current code/tests or committed evidence.

## Rule owners

| Rule area | Canonical owner | Discovery documents |
|---|---|---|
| Backend engineering invariants | [`../AGENTS.md`](../AGENTS.md) | [`.ai/README.md`](README.md), [`../docs/DOCS_INDEX.md`](../docs/DOCS_INDEX.md) |
| Smallest agent entry path and repository-root bootstrap | [`.ai/README.md`](README.md) | [`../AGENTS.md`](../AGENTS.md) |
| Time/context/read/search/change limits | [`.ai/TOKEN_BUDGETS.md`](TOKEN_BUDGETS.md) | [`.ai/README.md`](README.md), [`../docs/AGENT_SHARED_OPERATING_STANDARD.md`](../docs/AGENT_SHARED_OPERATING_STANDARD.md) |
| Validation selection | [`.ai/VALIDATION_SELECTOR.md`](VALIDATION_SELECTOR.md) | [`../AGENTS.md`](../AGENTS.md), [`../docs/BACKEND_CHANGE_CHECKLIST.md`](../docs/BACKEND_CHANGE_CHECKLIST.md) |
| Command timeouts, shell limits and guarded execution | [`../docs/AGENT_COMMAND_PLAYBOOK.md`](../docs/AGENT_COMMAND_PLAYBOOK.md) | [`.ai/VALIDATION_SELECTOR.md`](VALIDATION_SELECTOR.md) |
| New prompt contract and active admission | [`.ai/PROMPT_LINT_CHECKLIST.md`](PROMPT_LINT_CHECKLIST.md) | [`../docs/ai/TASK_TEMPLATE.md`](../docs/ai/TASK_TEMPLATE.md) |
| Queue discovery and canonical owner routing | [`../docs/prompt_queues/README.md`](../docs/prompt_queues/README.md) | [`../docs/DOCS_INDEX.md`](../docs/DOCS_INDEX.md) |
| Queue states, ownership and closure | [`../docs/prompt_queues/PROMPT_LIFECYCLE.md`](../docs/prompt_queues/PROMPT_LIFECYCLE.md) | [`../AGENTS.md`](../AGENTS.md) |
| Run evidence shape | [`.ai/RUN_LOG_TEMPLATE.md`](RUN_LOG_TEMPLATE.md) | [`../docs/AGENT_RUN_LOG_ENFORCEMENT.md`](../docs/AGENT_RUN_LOG_ENFORCEMENT.md) |
| Completion score caps and evidence enforcement | [`../docs/AGENT_RUN_LOG_ENFORCEMENT.md`](../docs/AGENT_RUN_LOG_ENFORCEMENT.md) | [`../docs/AGENT_SHARED_OPERATING_STANDARD.md`](../docs/AGENT_SHARED_OPERATING_STANDARD.md) |
| Mistake IDs and prevention loop | [`../docs/ai/learning/MISTAKE_LEDGER.md`](../docs/ai/learning/MISTAKE_LEDGER.md) | run logs and mistake-card template |
| Backend architecture | [`../docs/ARCHITECTURE_OVERVIEW.md`](../docs/ARCHITECTURE_OVERVIEW.md) | current source and tests |
| HTTP endpoint and auth contract | [`../docs/API_ENDPOINT_INVENTORY.md`](../docs/API_ENDPOINT_INVENTORY.md) | endpoint source and contract tests |
| Regression requirements by bug class | [`../docs/BUGFIX_PATTERN_GUARDRAILS.md`](../docs/BUGFIX_PATTERN_GUARDRAILS.md) | [`../docs/BACKEND_REGRESSION_GUARDRAILS.md`](../docs/BACKEND_REGRESSION_GUARDRAILS.md) |
| Mobile/backend contract handoff | [`../docs/mobile_contract_idempotency_handoff.md`](../docs/mobile_contract_idempotency_handoff.md) | Flutter contract docs and backend tests |
| Commit/push/direct-main policy | [`../AGENTS.md`](../AGENTS.md) | queue lifecycle and run evidence |

## Update rule

When a workflow rule changes:

1. update the canonical owner first;
2. update this map only when ownership or the default entrypoint changes;
3. replace copied mechanics elsewhere with a short summary and link;
4. update validators/tests when the rule is mechanically enforceable;
5. update `docs/DOCS_INDEX.md` when discovery changes;
6. do not create a second owner merely to make a prompt self-contained.

## Current non-negotiable rules

- Prove the backend repository root before prompt discovery or local execution.
- One prompt has one bounded outcome, one primary lane and one authoritative runtime owner.
- New non-trivial prompts use `Prompt contract: v2`; newly active prompts additionally use `Prompt admission: v3`.
- Historical prompts are not declared modern merely because they were copied or reformatted.
- Use the strictest applicable time/context limit; reaching a limit triggers handoff, not silent expansion.
- Blocking `dotnet`, Python test, network Git and GitHub CLI commands use the guarded runner.
- Authenticated server identity is authoritative for mobile-facing writes.
- Idempotency and economy settlement require stable operation identity and exactly-once evidence.
- Migration changes require mapping/snapshot alignment and provider-appropriate proof.
- Docs-only audits cannot claim runtime fixes.
- Local edits, commits, pushed branches and open PRs are delivery states, not proof of completion.
- Connector-only work cannot claim local commands that did not execute.

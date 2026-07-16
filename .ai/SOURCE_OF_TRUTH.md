# Backend AI Workflow Source-of-Truth Map

Last aligned: 2026-07-17  
Owner: `backend-agent-system`

Short entrypoints may summarize a rule but may not redefine it. Current code, focused tests and executable tooling override prose.

## Authority order

1. Current backend code, focused tests and executable tooling.
2. This owner map.
3. The canonical owner below.
4. Short entrypoints/indexes.
5. Dated audits, queues, evidence logs and chat history.

## Rule owners

| Rule area | Canonical owner | Fast discovery |
|---|---|---|
| Backend engineering invariants | [`../AGENTS.md`](../AGENTS.md) | [`.ai/README.md`](README.md) |
| 60-second task start and root proof | [`.ai/README.md`](README.md) | `scripts/agent_run.py` |
| Time/read/search/edit limits | [`.ai/TOKEN_BUDGETS.md`](TOKEN_BUDGETS.md) | `scripts/agent_run.py plan` |
| Validation selection | [`.ai/VALIDATION_SELECTOR.md`](VALIDATION_SELECTOR.md) | `scripts/agent_run.py plan` |
| Command timeout/safety | [`../docs/AGENT_COMMAND_PLAYBOOK.md`](../docs/AGENT_COMMAND_PLAYBOOK.md) | `scripts/run_guarded.py` |
| Formal prompt contract/admission | [`.ai/PROMPT_LINT_CHECKLIST.md`](PROMPT_LINT_CHECKLIST.md) | [`../docs/ai/TASK_TEMPLATE.md`](../docs/ai/TASK_TEMPLATE.md) |
| Queue discovery and states | [`../docs/prompt_queues/README.md`](../docs/prompt_queues/README.md), [`PROMPT_LIFECYCLE.md`](../docs/prompt_queues/PROMPT_LIFECYCLE.md) | user assignment bypasses discovery |
| Compact run evidence | [`.ai/RUN_LOG_TEMPLATE.md`](RUN_LOG_TEMPLATE.md) | `scripts/agent_run.py` |
| Evidence score/closure rules | [`../docs/AGENT_RUN_LOG_ENFORCEMENT.md`](../docs/AGENT_RUN_LOG_ENFORCEMENT.md) | changed-only validator |
| Mistake IDs/details | [`../docs/ai/learning/MISTAKE_LEDGER.md`](../docs/ai/learning/MISTAKE_LEDGER.md) | [`MISTAKE_INDEX.json`](../docs/ai/learning/MISTAKE_INDEX.json) selects cards |
| Throughput measurement | `scripts/analyze_agent_runs.py` | speed audit/CI summary |
| Expensive DB CI scope | `scripts/ci/classify_backend_changes.py` | `Database Validation` workflow |
| Architecture/contracts | owning backend docs and current tests | [`../docs/DOCS_INDEX.md`](../docs/DOCS_INDEX.md) |

## Update rule

When a workflow rule changes:

1. update its canonical owner;
2. update executable validators/tests;
3. update this map only if ownership/discovery changed;
4. replace duplicated mechanics elsewhere with a link;
5. keep dated audits as evidence, not policy.

## Non-negotiable throughput rules

- User-assigned bounded work does not require queue discovery or prompt admission.
- Agents use `MISTAKE_INDEX.json`; they do not read the whole ledger by default.
- New evidence uses compact v2 and records numeric throughput metrics.
- `Commit SHA: self` is valid; no SHA-backfill commit.
- Changed evidence validation must not scan unrelated historical debt.
- One run has one lane and one authoritative owner.
- Oversized/mixed tasks split before implementation.
- Docs/agent-tooling-only changes skip the expensive database suite through the classifier.
- Required proof, not checklist length, determines completion.

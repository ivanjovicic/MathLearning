# DIRECT-CURSOR-EXECUTION-EFFICIENCY — Evidence

Date: 2026-09-12
Queue: direct-user-request
Run mode: docs-evidence
Token budget: low
Commit SHA: self

## Outcome

Add durable repository guidance that keeps Cursor/AI execution direct by default and limits unnecessary subagent/context overhead without banning legitimate bounded audit/verification delegation.

## Evidence reviewed

- `AGENTS.md`
- `.ai/README.md`
- `.ai/TOKEN_BUDGETS.md`
- `docs/AGENT_SHARED_OPERATING_STANDARD.md`
- current Cursor documentation for Rules and Subagents
- repository check confirmed no pre-existing `.cursor` project rules/agents before this change

## Changes

- Added `.cursor/rules/agent-execution-efficiency.mdc` as an always-applied Cursor project rule.
- Updated `.ai/TOKEN_BUDGETS.md` with direct-by-default execution topology, per-budget subagent limits, delegation gate, no-nesting/no-duplicate-research rules, waiting/progress behavior and delegation metrics.
- Preserved existing ownership, validation, queue and delivery rules; the new policy only narrows execution topology.

## Key policy

- `micro` / `low`: zero subagents.
- `medium`: at most one bounded subagent when justified.
- `high` finite audit: at most one by default; more requires an explicitly parallel prompt.
- Routine search/read/shell/test/implementation/docs work stays in the main agent.
- Subagent work counts against the same run budget and does not reset context/time limits.
- Nested/recursive delegation and duplicated research are disallowed.

## Validation

- Repository content was inspected through the GitHub connector after reading current main guidance.
- Cursor rule format was checked against current Cursor Rules documentation (`.cursor/rules/*.mdc`, YAML frontmatter, `alwaysApply: true`).
- No local repository commands were run because this change was performed connector-only.
- Runtime/product tests: not run — docs/rules-only change.

Documentation impact: updated `.ai/TOKEN_BUDGETS.md`; added `.cursor/rules/agent-execution-efficiency.mdc` and this evidence log.

## Delivery

- Cursor rule commit: `d1e9a128fc3d9cba375f37945e967d0ee96d5982`
- Budget policy commit: `4dfbf02d8ab0d88d6963d519854610aa97411be0`
- Evidence commit: self
- Target: `main`

## Residual risk

Cursor may still internally choose implementation details, but repository instructions now explicitly constrain automatic delegation and make repeated subagent use a budget/scope violation.

# BACKEND-DOCS-ROUTING-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-DOCS-ROUTING-001
Queue: user-assigned
Agent/tool: ChatGPT/GitHub connector + synthetic documentation fixture
Model provider: OpenAI
Model name/id: GPT-5.6 Thinking
Client/IDE: ChatGPT web
Run mode: docs-evidence
Token budget: high
Started at UTC: 2026-07-17T08:11:00Z
Completed at UTC: 2026-07-17T08:16:00Z
Elapsed time: 5m 0s
Relevant prior mistakes read: BACKEND-MISTAKE-QUEUE-001, BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes: current main/evidence/archive outrank stale Ready text, delivered IDs are not reopened and residual behavior gets a new narrower owner
Owner/hypothesis: backend-agent-system owns queue truth; current router is slow and unsafe because completed critical/migration/pass3 rows still look selectable
Files inspected: 18
Files changed: 9
Searches: 5
Validation runs: 2
Failed retries: 0

## Outcome
- Rewrote the backend router around current unresolved owners instead of all-Done historical queues.
- Added archive override semantics and a 2026-07-17 completed/superseded archive.
- Synchronized `BACKEND-MIGRATION-001` to Done and pass-3 rows `017..019` to delivered/nonclaimable states.
- Superseded partial `BACKEND-API-DB-016` with a narrower residual instead of reopening the broad prompt.
- Added explicit cross-repo handoff/deduplication rules to the lifecycle and durable rulebook/index.

## Changed paths
- `AGENTS.md`; `.ai/SOURCE_OF_TRUTH.md`; `docs/DOCS_INDEX.md`
- `docs/prompt_queues/PROMPT_LIFECYCLE.md`; `docs/prompt_queues/README.md`
- `docs/prompt_queues/completed_archive_2026_07_17.md`
- `docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`
- `docs/prompt_queues/backend_failing_test_followups_2026_07_11.md`; this log

## Validation
Validation run: synthetic documentation health and agent-system wiring passed after routing changes; local link targets in changed durable docs resolved in fixture
Validation not run: exact target-tree Git/PR/archive consistency requires PR workflow and connector verification

## Exceptions and learning
Mistakes observed: BACKEND-MISTAKE-QUEUE-001 repeated; prevention=archive override, new residual IDs and one current router
Waste: stale queue status had to be reconciled from run logs rather than queue rows
Missed: historical prompt bodies were preserved through Git history rather than mass-reformatted
Follow-up: `BACKEND-PR-DISPOSITION-001` owns stale draft PR #3
Residual risk: exact main commits in archive remain evidence pointers and must not be mistaken for new runtime validation
Documentation impact: updated rulebook, source map, durable index, lifecycle, router and status archives
Cross-repo impact: added handoff rules; no mobile runtime files changed

## Delivery
State: Needs merge
Branch/PR: agent/backend-docs-crossrepo-20260717 / pending
Commit SHA: self
Completion %: 92

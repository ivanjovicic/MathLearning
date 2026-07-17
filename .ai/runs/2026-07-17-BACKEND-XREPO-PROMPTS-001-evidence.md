# BACKEND-XREPO-PROMPTS-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-XREPO-PROMPTS-001
Queue: user-assigned
Agent/tool: ChatGPT/GitHub connector + current-main cross-repo research
Model provider: OpenAI
Model name/id: GPT-5.6 Thinking
Client/IDE: ChatGPT web
Run mode: docs-evidence
Token budget: high
Started at UTC: 2026-07-17T08:16:00Z
Completed at UTC: 2026-07-17T08:20:00Z
Evidence synchronized at UTC: 2026-07-17T09:07:47Z
Elapsed time: 4m 0s
Relevant prior mistakes read: BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-QUEUE-001, BACKEND-MISTAKE-IDEM-002, BACKEND-MISTAKE-EVIDENCE-001
How this run avoids prior mistakes: inspect both main baselines, reuse `BE-PERF-011/012/015`, create only uncovered owners and require exact timeout/replay/privacy/provider evidence
Owner/hypothesis: current-main backend/Flutter contract gaps can be split into one adaptive-start owner, two screenshot phases and one review-only stale-PR disposition
Files inspected: 21
Files changed: 7
Searches: 6
Validation runs: 5
Failed retries: 0

## Outcome
- Added a current-main cross-repo queue with backend baseline `76693a1d...` and Flutter baseline `0d01e940...`.
- Added a detailed adaptive session-start idempotency prompt that reuses the shared ledger and explicitly excludes existing adaptive-answer/practice owners.
- Split screenshot privacy/authorized streaming from provider/deployment durability into two dependent prompts.
- Added a review-only prompt for stale draft PR #3 so broad old tests are not merged wholesale.
- Strengthened formal prompt admission with both-repo baseline, existing-owner and synchronization requirements.

## Changed paths
- `.ai/PROMPT_LINT_CHECKLIST.md`
- `docs/prompt_queues/backend_cross_repo_current_main_2026_07_17.md`
- `docs/prompt_queues/backend_cross_repo/BACKEND-XREPO-ADAPTIVE-START-001.md`
- `docs/prompt_queues/backend_cross_repo/BACKEND-API-DB-020.md`
- `docs/prompt_queues/backend_cross_repo/BACKEND-API-DB-021.md`
- `docs/prompt_queues/backend_cross_repo/BACKEND-PR-DISPOSITION-001.md`; this log

## Validation
Validation run: all four prompts passed a local contract-equivalent field/list/lane/budget/command/forbidden-token check; every command is <=180 characters; documentation health passed in fixture
Validation run: PR-head `dd30d5497895e92be28a5290bfb3961935f238e5` workflow `Backend Agent System Validation` run `29567903813` completed successfully, including changed prompt admission and evidence validation
Validation run: PR-head workflow `Database Validation` run `29567903944` completed successfully with docs-only classification and intentional database-suite skip
Validation not run: no backend or Flutter runtime implementation was part of this prompt-authoring task

## Exceptions and learning
Mistakes observed: BACKEND-MISTAKE-XREPO-001 repeated; prevention=both-main baselines and existing-owner handoff table
Waste: none beyond current-main code/queue deduplication needed to avoid duplicate prompts
Missed: no runtime fixes, provider selection or PR #3 disposition were claimed
Follow-up: execute queue rows in priority/dependency order
Residual risk: No material residual risk
Documentation impact: added current-main cross-repo queue/prompts and admission rules
Cross-repo impact: Flutter prompts are dependencies/handoffs only; no Flutter repository change was made

## Delivery
State: Done
Branch/PR: `agent/backend-docs-crossrepo-20260717` / PR #8 merged
PR head SHA: `dd30d5497895e92be28a5290bfb3961935f238e5`
Merge/main SHA: `ff22caf79cecaad14d827fb2449021a1bafe63ec`
Commit SHA: self
Completion %: 100

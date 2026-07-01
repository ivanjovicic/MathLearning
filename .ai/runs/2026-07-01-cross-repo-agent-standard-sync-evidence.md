# Cross Repo Agent Standard Sync Evidence

Prompt ID: ad-hoc-cross-repo-agent-standard-sync  
Queue: docs/evidence/cross-repo standards  
Agent/tool: ChatGPT GitHub connector  
Model provider: OpenAI  
Model name/id: GPT-5.5 Thinking  
Model mode/settings: reasoning model, ChatGPT web with GitHub connector  
Client/IDE: ChatGPT web  
Run mode: docs/evidence  
Token budget: high  
Actual context: high  
Started from queue status: user requested comparing MathLearning backend, Flutter, and AgentsWatch docs and aligning useful agent/prompt rules  
Local collision check: not applicable - GitHub connector docs-only cross-repo update  
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001  
How this run avoids prior mistakes: added backend shared standard and cross-repo sync prompt; did not claim runtime code changed or tests passed  
Elapsed time: unknown-not-recorded  
Phase time breakdown: unknown-not-recorded

## Files inspected

- `AGENTS.md`
- `docs/DOCS_INDEX.md`
- `docs/AGENT_RUN_LOG_ENFORCEMENT.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `.ai/RUN_LOG_TEMPLATE.md`
- matching Flutter and AgentsWatch process docs through GitHub connector

## Files changed in this repo

- `docs/AGENT_SHARED_OPERATING_STANDARD.md`
- `docs/ai/prompts/CROSS_REPO_AGENT_STANDARD_SYNC_PROMPT.md`
- `.ai/runs/2026-07-01-cross-repo-agent-standard-sync-evidence.md`

## Commands run

- GitHub fetch_file / create_file via connector.

## What was done

- Added a backend copy of the shared cross-repo agent operating standard.
- Added a reusable cross-repo standard sync prompt.
- Recorded this docs-only evidence log.

## What was missed

- Did not update full `AGENTS.md` / `DOCS_INDEX.md` references in this pass.
- Did not run local `git diff --check` because this used GitHub connector writes.
- Did not run backend tests because no runtime code changed.

## Validation run

- Source docs were fetched before writing.
- GitHub create_file returned commit SHAs.

## Validation not run

- `git diff --check` not run locally.
- `dotnet test` not run; docs-only change.

## Waste categories

- cross-repo rules were scattered across three repos;
- full index/rulebook reference sync deferred to a focused follow-up.

## Mistakes observed

- Mistake ID: BACKEND-MISTAKE-XREPO-001
- New or repeated: repeated risk mitigated
- Root cause: shared rules and sync decisions were not equally visible across repos
- Prevention added: shared standard and cross-repo sync prompt added to backend
- Existing rule that should have prevented it: backend contract/workflow must record cross-repo sync
- Did this run update a rule/prompt/test/queue: yes, added docs and prompt

## Where time/context was wasted

- Comparing already-similar Flutter/backend standards and missing AgentsWatch gate files.

## Why waste happened

- Backend had many of the Flutter-style rules already, but the common standard was not named in one shared document.

## What the next agent should avoid

- Do not repeat a full comparison before reading `docs/AGENT_SHARED_OPERATING_STANDARD.md`.
- Do not claim backend runtime change or test pass from this docs-only sync.

## Docs/rules updated to prevent repeat

- `docs/AGENT_SHARED_OPERATING_STANDARD.md`
- `docs/ai/prompts/CROSS_REPO_AGENT_STANDARD_SYNC_PROMPT.md`

## Queue updated

- none

## New optimized prompt added

- `docs/ai/prompts/CROSS_REPO_AGENT_STANDARD_SYNC_PROMPT.md`

## Follow-up prompt

- Focused AGENTS/DOCS_INDEX reference sync across all three repos.

## Completion %

82%

## Residual risk

- Shared standard exists but full `AGENTS.md` and `DOCS_INDEX.md` references are not fully synced yet.

## Commit SHA

Related backend commits: `eb5690d`, `a130905`.

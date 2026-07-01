# Cross Repo Agent Standard Reference Sync Evidence

Prompt ID: ad-hoc-cross-repo-agent-standard-reference-sync  
Queue: docs/evidence/cross-repo standards  
Agent/tool: ChatGPT GitHub connector  
Model provider: OpenAI  
Model name/id: GPT-5.5 Thinking  
Model mode/settings: reasoning model, ChatGPT web with GitHub connector  
Client/IDE: ChatGPT web  
Run mode: docs/evidence  
Token budget: high  
Actual context: high  
Started from queue status: follow-up to align three repos after shared standard bootstrap  
Local collision check: not applicable  
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001  
How this run avoids prior mistakes: updated backend first-read docs so shared standard, evidence gate, and mistake ledger are visible before runtime work  
Elapsed time: unknown-not-recorded  
Phase time breakdown: unknown-not-recorded

## Files inspected

- `AGENTS.md`
- `docs/DOCS_INDEX.md`
- `docs/AGENT_SHARED_OPERATING_STANDARD.md`
- `docs/AGENT_RUN_LOG_ENFORCEMENT.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`

## Files changed

- `AGENTS.md`
- `docs/DOCS_INDEX.md`
- `.ai/runs/2026-07-01-cross-repo-agent-standard-reference-sync-evidence.md`

## Commands run

- GitHub fetch_file / update_file / create_file via connector.

## What was done

- Added `docs/AGENT_SHARED_OPERATING_STANDARD.md` to backend AGENTS read order.
- Added shared standard, run-log template, run-log README, and mistake ledger to backend DOCS_INDEX source-of-truth order.
- Added the cross-repo standard sync prompt to backend evidence docs list.
- Preserved backend-specific idempotency, auth scope, migration, contract, and test rules.

## What was missed

- Local `git diff --check` not run.
- Backend tests not run because no runtime code changed.
- Queue rows not changed.

## Validation run

- Source docs fetched before editing.
- GitHub writes returned commit SHAs.

## Validation not run

- `git diff --check` not run locally.
- `dotnet test` not run; docs-only change.

## Waste categories

- stale docs reference risk
- repeated context reads across repos
- connector-only validation limitation

## Mistakes observed

- Mistake ID: BACKEND-MISTAKE-XREPO-001
- New or repeated: repeated risk mitigated
- Root cause: shared standard was not in the top backend read order
- Prevention added: updated AGENTS and DOCS_INDEX
- Existing rule that should have prevented it: contract work must record cross-repo sync
- Did this run update a rule/prompt/test/queue: yes, AGENTS and DOCS_INDEX

## Follow-up prompt

- Run evidence lint after next 3-5 prompt-system changes.

## Completion %

88%

## Residual risk

- Local diff validation and backend tests were not run because this was connector-based docs-only work.

## Commit SHA

Backend commits: `4bc5963`, `d670bcc`.

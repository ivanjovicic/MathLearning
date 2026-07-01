# Mechanical Agent Evidence Validation Gate Evidence

Prompt ID: ad-hoc-mechanical-agent-evidence-validation-gate  
Queue: docs/evidence/cross-repo standards  
Agent/tool: ChatGPT GitHub connector  
Model provider: OpenAI  
Model name/id: GPT-5.5 Thinking  
Model mode/settings: reasoning model, ChatGPT web with GitHub connector  
Client/IDE: ChatGPT web  
Run mode: docs/evidence  
Token budget: high  
Actual context: high  
Started from queue status: user requested doing everything needed and improving further  
Local collision check: not applicable  
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUDIT-001  
How this run avoids prior mistakes: added backend validator script and shared-standard requirement to run it for evidence/prompt-system changes  
Elapsed time: unknown-not-recorded  
Phase time breakdown: unknown-not-recorded

## Files inspected

- `docs/AGENT_SHARED_OPERATING_STANDARD.md`
- Flutter `scripts/validate_agent_evidence.py` through connector
- backend evidence docs through connector

## Files changed

- `scripts/validate_agent_evidence.py`
- `docs/AGENT_SHARED_OPERATING_STANDARD.md`
- `.ai/runs/2026-07-01-mechanical-agent-evidence-validation-gate-evidence.md`

## Commands run

- GitHub search/fetch_file/create_file/update_file via connector.

## What was done

- Added backend `scripts/validate_agent_evidence.py` adapted to `BACKEND-MISTAKE-*` IDs.
- Added shared-standard section requiring `python scripts/validate_agent_evidence.py` for evidence/prompt-system changes.
- Added explicit connector-only skip wording.

## What was missed

- Did not run local Python validator because connector-only session has no full local checkout.
- Did not add CI workflow before local output is triaged.

## Validation run

- GitHub writes returned commit SHAs.

## Validation not run

- not run - connector-only docs/tooling update, no local checkout.

## Waste categories

- tool limitation
- connector-only validation unavailable

## Mistakes observed

- Mistake ID: BACKEND-MISTAKE-EVIDENCE-001
- New or repeated: repeated risk mitigated
- Root cause: backend evidence rules were documented but not fully mechanical
- Prevention added: backend validator script and shared-standard command requirement
- Existing rule that should have prevented it: run-log enforcement and evidence lint prompt
- Did this run update a rule/prompt/test/queue: yes, script and shared standard

## Completion %

90%

## Residual risk

- Validator was added but not executed in this connector-only session.

## Commit SHA

Backend commits: `f2f3fec`, `8914486`.

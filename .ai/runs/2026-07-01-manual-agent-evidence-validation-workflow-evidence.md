# Manual Agent Evidence Validation Workflow Evidence

Prompt ID: ad-hoc-manual-agent-evidence-validation-workflow  
Queue: docs/evidence/cross-repo standards  
Agent/tool: ChatGPT GitHub connector  
Model provider: OpenAI  
Model name/id: GPT-5.5 Thinking  
Client/IDE: ChatGPT web  
Run mode: docs/evidence  
Token budget: medium  
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001  
Elapsed time: unknown-not-recorded  
Phase time breakdown: unknown-not-recorded

## Files inspected

- `scripts/validate_agent_evidence.py`
- `docs/AGENT_SHARED_OPERATING_STANDARD.md`

## Files changed

- `.github/workflows/agent-evidence-validation.yml`
- `.ai/runs/2026-07-01-manual-agent-evidence-validation-workflow-evidence.md`

## What was done

- Added manual GitHub Actions workflow for backend agent evidence validation.
- Workflow supports `referenced`, `all`, and `strict-legacy` modes.
- Workflow is manual-only to avoid breaking existing branches before legacy evidence is triaged.

## Validation not run

- not run - connector-only docs/tooling update, no local checkout.

## Mistakes observed

- Mistake ID: BACKEND-MISTAKE-EVIDENCE-001
- New or repeated: repeated risk mitigated
- Root cause: backend validation script had no GitHub UI entry point
- Prevention added: manual workflow added
- Existing rule that should have prevented it: mechanical evidence validation gate
- Did this run update a rule/prompt/test/queue: yes, workflow

## Completion %

92%

## Residual risk

- Workflow was added but not executed from GitHub Actions in this session.

## Commit SHA

- `941c26b`

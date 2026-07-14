# BACKEND-LATEST-EVIDENCE-002 Evidence

Prompt ID: BACKEND-LATEST-EVIDENCE-002
Queue: docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md
Agent/tool: Codex desktop
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: Codex desktop
Run mode: docs/evidence repair only
Token budget: unknown-not-exposed
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded
Started from queue status: Prompt-ready
Local collision check: git status already dirty with existing user/agent changes; no new collision introduced yet
Relevant prior mistakes read:
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-QUEUE-001
How this run avoids prior mistakes:
- lint the referenced evidence logs before changing any queue status language
- keep runtime/test code untouched
- reconcile only durable docs/evidence fields that overstate validation or completion

## Files inspected

- `scripts/validate_agent_evidence.py`
- `.ai/runs/2026-07-03-BACKEND-TEST-AUDIT-003-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-PERF-AUDIT-004-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-024-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-028-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-029-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-030-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-035-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-036-evidence.md`
- `docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md`
- `docs/prompt_queues/backend_test_coverage.md`

## Files changed

- `.ai/runs/2026-07-03-BACKEND-TEST-AUDIT-003-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-PERF-AUDIT-004-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-024-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-028-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-029-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-030-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-035-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-036-evidence.md`
- `.ai/runs/2026-07-13-BACKEND-LATEST-EVIDENCE-002-evidence.md`
- `.ai/runs/2026-07-13-BACKEND-LATEST-WORKFLOW-002-evidence.md`

## Commands run

- `python scripts/validate_agent_evidence.py --referenced-run-logs-only`
- `git log --oneline -- .ai/runs/2026-07-03-BACKEND-TEST-AUDIT-003-evidence.md`
- `git log --oneline -- .ai/runs/2026-07-03-BACKEND-PERF-AUDIT-004-evidence.md`
- `git log --oneline -- .ai/runs/2026-07-03-BACKEND-TEST-024-evidence.md .ai/runs/2026-07-03-BACKEND-TEST-028-evidence.md .ai/runs/2026-07-03-BACKEND-TEST-029-evidence.md .ai/runs/2026-07-03-BACKEND-TEST-030-evidence.md .ai/runs/2026-07-03-BACKEND-TEST-035-evidence.md .ai/runs/2026-07-03-BACKEND-TEST-036-evidence.md`

## What was done

- Ran the referenced-only evidence validator to audit the latest run logs and queue references.
- Pulled the actual evidence SHAs from git history so the July 3 logs could record a real `Commit SHA:` field instead of only listing key commits.
- Lowered the overstated completion scores on the unvalidated July 3 logs to stay within the evidence cap.
- Kept runtime and test files untouched.

## What was missed

- The repository-wide referenced lint still reports pre-existing legacy queue/log debt outside the July 3 evidence set.
- I did not repair the older June 24 / July 1 evidence backlog because that is a larger cleanup than this prompt safely covers.

## Validation run

- `python scripts/validate_agent_evidence.py --referenced-run-logs-only` still fails with 140 failures and 4 warnings, but the remaining findings are from older legacy evidence rows/logs outside the July 3 audit set.

## Validation not run

- Full repository evidence lint remained failing because of pre-existing legacy rows/logs.

## Waste categories

- Evidence-format drift.
- Legacy queue/log debt.
- Time spent separating current July 3 evidence from older June/July failures.

## Mistakes observed

Mistakes observed: none

## Where time/context was wasted

- The referenced-only validator is dominated by old queue and log rows that are not part of the July 3 evidence set.

## Why waste happened

- The repository still carries older evidence debt, so a clean audit of the latest logs is mixed with unrelated historical failures.

## What the next agent should avoid

- Treating the remaining 140 validator findings as a failure of the July 3 logs I just repaired.
- Assuming a queue row is clean just because the latest prompt-level evidence was updated.

## Docs/rules updated to prevent repeat

- `docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md`
- `docs/prompt_queues/backend_test_coverage.md`

## Queue updated

- `BACKEND-LATEST-EVIDENCE-002` marked `Done 75%` with a run log path and residual legacy-debt note.
- `BACKEND-LATEST-EVIDENCE-002` now has a run log that captures the validator result and the score-cap reconciliation.
- `BACKEND-LATEST-WORKFLOW-002` run-log completion was reduced to match the failed workflow evidence.
- The July 3 evidence logs for 024, 028, 029, 030, 035 and 036 now include explicit `Commit SHA:` fields.

## New optimized prompt added

- None.

## Follow-up prompt

BACKEND-LATEST-QUEUE-002

## Completion %

75%

## Residual risk

Referenced-only lint still reports older legacy queue/log debt outside the July 3 evidence set.

Commit SHA: 9b01a629e7571375986d85dce8075652fc680ad8

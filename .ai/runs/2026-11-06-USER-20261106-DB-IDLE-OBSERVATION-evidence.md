# USER-20261106-DB-IDLE-OBSERVATION Evidence

Evidence format: v2
Prompt ID: USER-20261106-DB-IDLE-OBSERVATION
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: validation-only
Token budget: medium
Started at UTC: 2026-11-06T18:51:35Z
Completed at UTC: 2026-11-06T18:54:40Z
Elapsed time: 3m 5s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: The delivered PreProductionIdle profile should permit Fly auto-stop and a Neon suspend window; existing read-only platform access may let us capture a no-traffic observation. Falsifier: no authenticated read-only platform/metrics access or active traffic prevents a valid idle sample; do not deploy or alter configuration.
Files inspected: 9
Files changed: 2
Searches: 1
Validation runs: 8
Failed retries: 0

## Outcome
- Read-only Fly API reports mathlearning-api suspended with its ams machine stopped. Updated the cost queue with this partial live evidence and the remaining Neon telemetry blocker.

## Changed paths
- docs/prompt_queues/backend_db_cost_guardrails_2026_09_21.md
- .ai/runs/2026-11-06-USER-20261106-DB-IDLE-OBSERVATION-evidence.md

## Validation
Validation run: fly auth whoami: exit 0; fly status --app mathlearning-api --json: Status=suspended, machine=stopped in ams; fly machine list --app mathlearning-api --json: machine=stopped; git diff --check: pass; python -B scripts/check_documentation_health.py --full-links: pass (28 documents, 0 issues); python -B scripts/validate_agent_prompt.py --changed-from 9841edbf6eded9e5d91a383cbbeacd06035863ad: pass (0 active prompts changed); python -B scripts/validate_agent_system.py: pass (0 issues).
Validation not run: Neon compute-idle metrics and an idle-window sample unavailable: neonctl and Neon telemetry connector are absent, with no Neon API credential name exposed in the environment; runtime tests not run because no code changed.

## Exceptions and learning
Mistakes observed: none
Waste: none
Missed: none
Follow-up: Operator with read-only Neon metrics access should record compute active/suspended state through a no-traffic idle window; retain BACKEND-OPS-DB-IDLE-BUDGET-001 at 95% until then.
Residual risk: Fly stopped-state evidence does not prove the Neon compute entered its idle/suspend window.
Documentation impact: updated docs/prompt_queues/backend_db_cost_guardrails_2026_09_21.md with timestamped Fly state and the exact Neon proof gap
Cross-repo impact: no - no client contract changed

## Delivery
State: Needs validation
Branch/PR: codex/fix-sunday-weekly-xp-tests; direct push to origin/main after documentation validation
Commit SHA: self
Completion %: 79

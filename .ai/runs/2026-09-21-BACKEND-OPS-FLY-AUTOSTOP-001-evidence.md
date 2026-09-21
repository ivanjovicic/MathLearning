# BACKEND-OPS-FLY-AUTOSTOP-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-OPS-FLY-AUTOSTOP-001
Queue: docs/prompt_queues/backend_db_cost_guardrails_2026_09_21.md
Agent/tool: GPT-5.6-Luna
Model provider: OpenAI
Model name/id: GPT-5.6
Client/IDE: Cursor
Run mode: docs-evidence
Token budget: low
Started at UTC: 2026-09-21T20:11:13Z
Completed at UTC: 2026-09-21T20:11:52Z
Elapsed time: 0m 39s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: Fly lacks explicit stop/start policy; falsifier is an existing auto-stop config or static validation proving all required settings already exist.
Files inspected: 6
Files changed: 2
Searches: 2
Validation runs: 4
Failed retries: 1

## Outcome
- Enabled explicit pre-production Fly auto-stop/auto-start with zero minimum warm machines and documented cold-start/operator verification behavior.

## Changed paths
- fly.toml
- FLY_DEPLOYMENT_GUIDE.md

## Validation
Validation run: TOML parser and autostop assertions pass; git diff --check pass; documentation health pass (25 documents, 0 failures).
Validation not run: fly config validate/deploy/status and live stop/start observation not run because fly CLI is unavailable; these remain operator deployment follow-up.

## Exceptions and learning
Mistakes observed: Applied BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003 and BACKEND-MISTAKE-SCOPE-001.
Waste: Initial TOML assertion treated [[services]] as a table; corrected to validate the first service entry.
Missed: No live Fly deployment observation in this environment.
Follow-up: Run fly config validate, deploy, status and observe idle auto-stop plus request-triggered auto-start before claiming production cost savings.
Residual risk: Config is delivered but runtime cost savings are not yet observed in Fly.
Documentation impact: Updated FLY_DEPLOYMENT_GUIDE.md with pre-production idle policy and operator verification commands.
Cross-repo impact: None.

## Delivery
State: Needs validation
Branch/PR: cursor/backend-ops-fly-autostop-001-8371 head 9cc95f3; pending main delivery
Commit SHA: self
Completion %: 79

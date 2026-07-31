# BACKEND-TEST-FIRST-STANDARD-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-TEST-FIRST-STANDARD-001
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: docs-evidence
Token budget: high
Started at UTC: 2026-07-31T01:01:10Z
Completed at UTC: 2026-07-31T01:03:27Z
Elapsed time: 2m 17s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-PROCESS-002
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-AUDIT-001; apply BACKEND-MISTAKE-PROCESS-002
Owner/hypothesis: open
Files inspected: 18
Files changed: 8
Searches: 6
Validation runs: 6
Failed retries: 2

## Outcome
- Added forward-only test-first contract rules to runtime prompts and explicit exceptions for audit/docs lanes
- Added validator checks plus regression tests for missing runtime proof and valid audit exception
- Updated bugfix, lifecycle, validation, evidence and agent rule owners

## Changed paths
- scripts/validate_agent_prompt.py
- scripts/test_validate_agent_prompt.py
- .ai/PROMPT_LINT_CHECKLIST.md
- .ai/VALIDATION_SELECTOR.md
- docs/ai/TASK_TEMPLATE.md
- docs/BUGFIX_PATTERN_GUARDRAILS.md
- docs/prompt_queues/PROMPT_LIFECYCLE.md
- docs/AGENT_RUN_LOG_ENFORCEMENT.md

## Validation
Validation run: python -m unittest -v scripts/test_validate_agent_prompt.py: passed (8 tests) | python scripts/validate_agent_prompt.py --changed-from origin/main: pending until staged/committed | python scripts/check_documentation_health.py --write-registry: passed | git diff --check: passed with CRLF warnings only
Validation not run: No GitHub Actions evidence found via connector

## Exceptions and learning
Mistakes observed: none
Waste: patch retries caused by markdown backtick escaping
Missed: Full changed-scope validator proof is completed after prompt migration and staging
Follow-up: BACKEND-TEST-FIRST-PROMPT-MIGRATION-001
Residual risk: Validator checks contract markers; runtime owners must still execute and retain the red and green commands
Documentation impact: updated .ai/PROMPT_LINT_CHECKLIST.md, .ai/VALIDATION_SELECTOR.md, docs/ai/TASK_TEMPLATE.md, docs/BUGFIX_PATTERN_GUARDRAILS.md, docs/prompt_queues/PROMPT_LIFECYCLE.md and docs/AGENT_RUN_LOG_ENFORCEMENT.md
Cross-repo impact: no

## Delivery
State: Done
Branch/PR: direct main
Commit SHA: self
Completion %: 95

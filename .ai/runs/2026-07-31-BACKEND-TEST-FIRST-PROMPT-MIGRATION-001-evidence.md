# BACKEND-TEST-FIRST-PROMPT-MIGRATION-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-TEST-FIRST-PROMPT-MIGRATION-001
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: docs-evidence
Token budget: high
Started at UTC: 2026-07-31T01:03:32Z
Completed at UTC: 2026-07-31T01:03:56Z
Elapsed time: 0m 24s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-PROCESS-002
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-AUDIT-001; apply BACKEND-MISTAKE-PROCESS-002
Owner/hypothesis: backend-quality owns active prompt bodies; hypothesis was that each current runtime owner can state a focused test-first proof while investigation/review owners remain honest exceptions.
Files inspected: 20
Files changed: 9
Searches: 6
Validation runs: 5
Failed retries: 0

## Outcome
- Added red/green/counterexample contracts to current runtime queue prompts
- Added explicit test-first exceptions to investigation and review prompts
- Updated BACKEND-ANALYSIS-001 so future routed runtime prompts inherit the gate

## Changed paths
- docs/BACKEND_CODE_ANALYSIS_PLAYBOOK.md
- docs/prompt_queues/BACKEND-ANALYSIS-001-periodic-code-analysis.md
- docs/prompt_queues/backend_season_authority/BACKEND-SEASON-XP-SETTLEMENT-001.md
- docs/prompt_queues/backend_season_authority/BACKEND-SEASON-TRACK-AUTHORITY-001.md
- docs/prompt_queues/backend_season_authority/BACKEND-SEASON-DAILY-RUN-PROVENANCE-001.md
- docs/prompt_queues/backend_cross_repo/BACKEND-XREPO-ADAPTIVE-START-001.md
- docs/prompt_queues/backend_cross_repo/BACKEND-API-DB-020.md
- docs/prompt_queues/backend_cross_repo/BACKEND-API-DB-021.md
- docs/prompt_queues/backend_cross_repo/BACKEND-PR-DISPOSITION-001.md

## Validation
Validation run: python scripts/validate_agent_prompt.py --changed-from origin/main: pending until staged/committed | python scripts/validate_agent_prompt.py on runtime and investigation prompts: passed | python -m unittest -v scripts/test_validate_agent_prompt.py: passed | git diff --check: passed with CRLF warnings only
Validation not run: No GitHub Actions evidence found via connector

## Exceptions and learning
Mistakes observed: none
Waste: none
Missed: No runtime implementation or focused .NET behavior test belongs to this docs/prompt migration
Follow-up: Each runtime prompt owner must execute its red proof before implementation
Residual risk: Prompt contracts can require proof but cannot manufacture a failing runtime test
Documentation impact: updated analysis playbook and nine active formal prompt bodies
Cross-repo impact: no

## Delivery
State: Done
Branch/PR: direct main
Commit SHA: self
Completion %: 95

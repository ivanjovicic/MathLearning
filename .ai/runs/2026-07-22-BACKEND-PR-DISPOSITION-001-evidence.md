# BACKEND-PR-DISPOSITION-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-PR-DISPOSITION-001
Queue: docs/prompt_queues/backend_cross_repo_current_main_2026_07_17.md
Agent/tool: Codex / mcp__codex_apps__github + functions.exec_command + web
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: review
Token budget: low
Started at UTC: 2026-07-22T07:15:50Z
Completed at UTC: 2026-07-22T07:17:44Z
Elapsed time: 1m 54s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-IDEM-001, BACKEND-MISTAKE-IDEM-002
How this run avoids prior mistakes: map every changed PR hunk to a current-main equivalent before disposing the stale draft, and record the exact close/supersede action in evidence.
Owner/hypothesis: Review/disposition owns PR #3 classification and the hypothesis is that current main already covers the useful invariants, leaving no unique still-needed test package.
Files inspected: 11
Files changed: 2
Searches: 6
Validation runs: 3
Failed retries: 0

## Outcome
- PR #3 was stale relative to current main and was closed.
- Every changed hunk mapped to current-main coverage or old scaffolding, so no unique still-needed test package remained to extract.
- The review comment and PR close recorded the disposition directly on GitHub.

## Changed paths
- .ai/runs/2026-07-22-BACKEND-PR-DISPOSITION-001-evidence.md
- docs/prompt_queues/backend_cross_repo_current_main_2026_07_17.md

## Validation
Validation run: `python scripts/validate_agent_prompt.py docs/prompt_queues/backend_cross_repo/BACKEND-PR-DISPOSITION-001.md` -> passed; `python scripts/check_documentation_health.py --full-links` -> passed (documents=24 failures=0); `python scripts/validate_agent_evidence.py --changed-from ccb9031f119c0fc518c3aefa7034914b990dbeff --verify-git` -> passed (failures=0 warnings=0)
Validation not run: none

## Exceptions and learning
Mistakes observed: none
Waste: stale PR diff chase resolved by mapping against current-main contract tests and explicit PR close
Missed: no GitHub Actions evidence found via connector
Follow-up: none
Residual risk: none
Documentation impact: updated the queue disposition row and review evidence log
Cross-repo impact: no

## Delivery
State: Done
Branch/PR: direct main
Commit SHA: self
Completion %: 79

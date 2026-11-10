# USER-20261110-CI-DOTNET-EF Evidence

Evidence format: v2
Prompt ID: USER-20261110-CI-DOTNET-EF
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: low
Started at UTC: 2026-11-10T15:04:45Z
Completed at UTC: 2026-11-10T15:13:37Z
Elapsed time: 8m 52s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-CI-001, BACKEND-MISTAKE-PROCESS-001
How this run avoids prior mistakes: applied BACKEND-MISTAKE-EVIDENCE-001; applied BACKEND-MISTAKE-VALIDATION-001; applied BACKEND-MISTAKE-CI-001; applied BACKEND-MISTAKE-PROCESS-001
Owner/hypothesis: Database validation installs unpinned dotnet-ef 11.0.0 under SDK 8 and crashes loading System.Runtime 10; pin the tool to the project's EF8 design version and prove the CI tool starts.
Files inspected: 11
Files changed: 3
Searches: 2
Validation runs: 10
Failed retries: 1

## Outcome
- Pinned database-validation dotnet-ef to EF Core 8.0.32 after PR 53 CI failed loading System.Runtime 10 from the unpinned EF 11 tool; added the CLI version log and synchronized the prior queue residual.

## Changed paths
- .github/workflows/database-validation.yml
- docs/prompt_queues/backend_migration_shape_guard.md
- .ai/runs/2026-11-10-USER-20261110-CI-DOTNET-EF-evidence.md

## Validation
Validation run: EF CLI 8.0.32 installed in an isolated temporary tool path and --version passed; backend change-classifier tests 7/7; docs contexts and full-links 28 documents/0 findings; agent-system validation 0 findings; final git diff and agent evidence verification with --verify-git passed.
Validation not run: Full PostgreSQL CI not run locally; workflow change requires the full database suite and will trigger it on the PR/push.

## Exceptions and learning
Mistakes observed: Applied BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-CI-001 and BACKEND-MISTAKE-PROCESS-001; pinned tooling to the repository EF version.
Waste: One evidence-validator retry to record full mistake IDs; final verification passed.
Missed: None.
Follow-up: Inspect PR 54 Database Validation after delivery; the workflow path forces a full suite, but CI is asynchronous and not a delivery gate.
Residual risk: Post-merge PostgreSQL 16 Database Validation is pending; focused local proof confirms the pinned EF CLI starts.
Documentation impact: Updated the BACKEND-MIGRATION-002 queue row to record the PR 53 CI toolchain failure and track its resolution through this follow-up.
Cross-repo impact: None.

## Delivery
State: Done
Branch/PR: Branch codex/ci-ef-tool-20261110; PR #54 planned; commit self.
Commit SHA: self
Completion %: 95

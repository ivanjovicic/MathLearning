# BACKEND-TEST-048 CLI health residual

Evidence format: v2
Prompt ID: BACKEND-TEST-048-CLI-HEALTH
Queue: user-assigned
Agent/tool: Codex
Model provider: OpenAI
Model name/id: gpt-6
Client/IDE: Codex desktop
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-11-15T15:11:25Z
Completed at UTC: 2026-11-15T16:18:26Z
Elapsed time: 67m 01s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: added CLI output regressions; kept `check` read-only; kept legacy maintenance ANALYZE-only; limited scope to #50 residual
Owner/hypothesis: PR #50's nullable bloat is safe in the service, but CLI formatting turns null into HEALTHY/blank and labels size-filtered stats as bloat; tests falsify this.
Files inspected: 19
Files changed: 6
Searches: 6
Validation runs: 16
Failed retries: 8

## Outcome
- CLI now renders null bloat as UNKNOWN; a measured numeric zero remains `0.00`.
- `check` separates physical bloat from index size, scans, and usage; zero scans display as UNUSED.
- `stats` no longer calls size-filtered rows bloated or classifies them HEALTHY/WATCH/NEEDS_REBUILD. Empty results say UNKNOWN, not zero bloat.
- Legacy `rebuild` output says ANALYZE-only and REINDEX performed: no.
- PR branch `aw-eval/ai-only-001-backend-test-048` was cleanly synchronized with current `origin/main` (`59dc54d`).
- PR branch owner matches the PR author; no other worktree, assignee, reviewer, or active claim/lease record was found.

## Changed paths
- `IndexMaintenanceTool/Program.cs`
- `IndexMaintenanceTool/IndexMaintenanceTool.csproj` (Npgsql aligned to required 8.0.6)
- `tests/MathLearning.Tests/MathLearning.Tests.csproj`
- `tests/MathLearning.Tests/Tools/IndexMaintenanceToolOutputTests.cs`
- `docs/API_ENDPOINT_INVENTORY.md`
- `.ai/runs/2026-11-15-BACKEND-TEST-048-CLI-HEALTH-evidence.md`

## Validation
Validation run: pre-fix CLI regressions failed 5/5 as expected; final focused CLI/SQL/endpoint tests passed 10/10; standalone CLI Release build passed with 0 warnings/errors; documentation health passed (28 documents, 0 failures); `git diff --check` passed; current-main/PR merge-tree had no conflicts.
Validation not run: PostgreSQL 16 provider tests could not start locally because Docker had no responding server; the guarded test attempt ended on idle timeout. Backend Agent System Validation failed on `978fc66` because completion was 90% with 6 searches against the medium limit of 4; this evidence now records 79% and Needs validation. Database Validation on `978fc66` was still running; both checks must be reviewed on the corrected head. Earlier PR-head CI success at `a7ec70f` is historical, not proof for the new head.

## Exceptions and learning
Mistakes observed: none
Waste: first full build exceeded its guard timeout; serialized Release build completed. No extension or infrastructure was added.
Missed: no local PostgreSQL 16 execution.
Follow-up: push the updated draft PR and inspect Database Validation plus Backend Agent System Validation for that exact head.
Residual risk: provider behavior is not yet verified on this head; no production maintenance commands, migrations, or REINDEX were run.
Documentation impact: updated the endpoint inventory to say the legacy POST runs ANALYZE only and disables REINDEX; routes and response contracts did not change.
Cross-repo impact: no.

## Delivery
State: Needs validation
Branch/PR: `aw-eval/ai-only-001-backend-test-048` -> draft PR #50
Commit SHA: self
Completion %: 79

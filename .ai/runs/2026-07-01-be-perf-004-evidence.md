# BE-PERF-004 Evidence (backfill)

Prompt ID: BE-PERF-004
Queue: docs/prompt_queues/backend_performance_optimization.md
Agent/tool: unknown-not-exposed (original); backfill by Cursor Agent
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: evidence backfill
Token budget: unknown-not-recorded
Actual context: unknown-not-recorded
Started from queue status: Done (queue incorrectly noted `docs-only`; commit is runtime perf)
Local collision check: none
Relevant prior mistakes read:
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001 (queue row mislabeled docs-only)
How this run avoids prior mistakes:
- Classify as runtime perf from `git show 851d961`
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Classification

runtime perf + test

## Files changed (commit `851d961`)

- `src/MathLearning.Infrastructure/Services/Leaderboard/DbBackedRedisLeaderboardService.cs`
- `tests/MathLearning.Tests/Services/DbBackedRedisLeaderboardServiceTests.cs`

## What was done (from commit)

- Replaced full ordered user-id materialization for rank with DB-side `CountAsync` over higher-scoring rows (`TryComputeRankAsync` / period tie-break).
- `GetNearRivalsAsync` reuses rank helper and fetches only a 5-row window.
- Added tie-breaker and near-rivals window tests.

## Tests added/changed in commit

- `GetUserRankAsync_TiebreakerUsesLexicographicUserId`
- `GetNearRivalsAsync_ReturnsFiveUserWindowAroundRank`

## Validation run

not run - backfill; queue cited `Leaderboard` filter for original work but execution unknown-not-recorded

## Validation not run

- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~Leaderboard"` — not proven for original run
- CI: No GitHub Actions evidence found via connector

## Waste categories

- none recorded (backfill)

## Mistakes observed

- Mistake ID: BACKEND-MISTAKE-EVIDENCE-001
- New or repeated: repeated (no original run log)
- Root cause: pre-bootstrap evidence gate
- Prevention added: this backfill log
- Existing rule that should have prevented it: AGENT_RUN_LOG_ENFORCEMENT.md
- Did this run update a rule/prompt/test/queue: yes — corrected queue `docs-only` label

- Mistake ID: BACKEND-MISTAKE-AUDIT-001
- New or repeated: repeated (queue said docs-only for runtime commit)
- Root cause: status drift between commit type and queue Notes
- Prevention added: queue row corrected to runtime perf + run log
- Did this run update a rule/prompt/test/queue: yes

## What was missed

- Original model/timing and test run proof

## Follow-up prompt

none

## Completion %

75%

## Residual risk

- Original model/time not recorded.
- Local test execution unknown unless proven.
- CI status unknown unless fetched.
- Redis-down leaderboard fallback should be re-validated with `DbBackedRedisLeaderboardServiceTests` after schema/index changes.

## Commit SHA

`851d961392b7d92f5b6fb99954d29bd889ce202a`

Cross-repo sync: not applicable

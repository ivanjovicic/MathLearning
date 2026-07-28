# BE-PERF-013 Evidence

Evidence format: v2
Prompt ID: BE-PERF-013
Queue: docs/prompt_queues/backend_performance_followups_2026_07_03.md
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-07-28T17:34:50Z
Completed at UTC: 2026-07-28T17:52:16.1623566Z
Elapsed time: 17m 26s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: GET/read paths still had hidden writes via streak roll, leaderboard refresh, and snapshot capture; remove them and prove the persisted state stays unchanged after reads.
Files inspected: 15
Files changed: 5
Searches: 9
Validation runs: 3
Failed retries: 1

## Outcome
- GET progress overview and SRS streak now read user profile state without rolling streaks or saving.
- School leaderboard list/history now read existing aggregates/snapshots without refresh, reward settlement, or snapshot capture on GET.
- Focused regression tests covered the stale-streak and empty-history counterexamples and passed after removing the last snapshot fallback.
- Release build of `MathLearning.slnx` passed after the read-path cleanup.

## Changed paths
- src/MathLearning.Api/Endpoints/ProgressEndpoints.cs
- src/MathLearning.Api/Endpoints/SrsEndpoints.cs
- src/MathLearning.Infrastructure/Services/LeaderboardService.cs
- src/MathLearning.Infrastructure/Services/StudentLeaderboardService.cs
- tests/MathLearning.Tests/Endpoints/ReadPathMutationRegressionTests.cs

## Validation
Validation run: `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~ReadPathMutationRegressionTests` (passed); `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet build MathLearning.slnx -c Release --no-restore` (passed)
Validation not run: none

## Exceptions and learning
Mistakes observed: BACKEND-MISTAKE-PERF-002 repeated; prevention=removed the remaining snapshot fallback from the school leaderboard history read path and added a regression test.
Waste: one failed focused test run before the last `CaptureSnapshotAsync` fallback was removed.
Missed: none
Follow-up: none
Residual risk: school leaderboard freshness is now owned by the background refresh job, so readers can see stale aggregates between refresh cycles, but reads no longer mutate.
Documentation impact: none - runtime-only fix
Cross-repo impact: no

## Delivery
State: Done
Branch/PR: direct main
Commit SHA: self
Completion %: 100

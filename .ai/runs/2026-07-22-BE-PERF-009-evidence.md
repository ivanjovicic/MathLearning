# BE-PERF-009 Evidence

Evidence format: v2
Prompt ID: BE-PERF-009
Queue: docs/prompt_queues/backend_performance_followups_2026_07_03.md
Agent/tool: Codex
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: Codex API
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-07-22T07:33:38Z
Completed at UTC: 2026-07-22T07:53:13Z
Elapsed time: 19m 35s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: bound the scheduler queue, coalesce duplicate user work, cap analysis history, and prove the bounded path with focused tests before claiming completion.
Owner/hypothesis: The weakness-analysis scheduler should stop unbounded growth by using a bounded queue with duplicate coalescing, explicit full-queue policy, and tests that prove duplicate suppression and cancellation behavior without changing the mobile contract.
Files inspected: 15
Files changed: 13
Searches: 10
Validation runs: 6
Failed retries: 4

## Outcome
- Weakness analysis now uses a bounded in-memory queue with duplicate suppression, explicit full-queue rejection, timeout-capped processing, and best-effort single-replica semantics.
- The analysis service now limits attempt history to the latest 1,000 attempts per user instead of materializing the full history.
- Focused regression tests passed for duplicate coalescing, capacity rejection, cancellation, and bounded-history query shape against a 100,000-attempt fixture.

## Changed paths
- `.ai/runs/2026-07-22-BE-PERF-009-evidence.md`
- `docs/prompt_queues/backend_performance_followups_2026_07_03.md`
- `src/MathLearning.Api/Services/PracticeAnalyticsUpdater.cs`
- `src/MathLearning.Api/Services/QuizAttemptIngestService.cs`
- `src/MathLearning.Api/Services/WeaknessAnalysisScheduler.cs`
- `src/MathLearning.Api/Services/WeaknessAnalysisService.cs`
- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- `src/MathLearning.Api/appsettings.json`
- `tests/MathLearning.Tests/Services/PracticeSessionServiceIntegrationTests.cs`
- `tests/MathLearning.Tests/Services/QuizAttemptIngestServiceRelationalTests.cs`
- `tests/MathLearning.Tests/Services/WeaknessAnalysisSchedulerTests.cs`
- `tests/MathLearning.Tests/Services/WeaknessAnalysisServiceIntegrationTests.cs`
- `tests/MathLearning.Tests/Services/WeaknessAnalysisServiceRelationalTests.cs`

## Validation
Validation run: `dotnet build MathLearning.slnx -c Release --no-restore` succeeded; `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --no-build --filter FullyQualifiedName~WeaknessAnalysisSchedulerTests` passed 3/3; `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~WeaknessAnalysisServiceRelationalTests` passed 1/1; `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~QuizAttemptIngestServiceRelationalTests|FullyQualifiedName~WeaknessAnalysisServiceIntegrationTests"` passed 10/10.
Validation not run: none

## Exceptions and learning
Mistakes observed: none
Waste: one PowerShell filter quoting retry and two compile-fix build loops.
Missed: none
Follow-up: none
Residual risk: best-effort in-memory ownership still loses queued work on restart or replica split, and the bounded lookback samples the latest 1,000 attempts instead of the full user history.
Documentation impact: updated `docs/prompt_queues/backend_performance_followups_2026_07_03.md`
Cross-repo impact: no

## Delivery
State: Done
Branch/PR: direct main
Commit SHA: self
Completion %: 100

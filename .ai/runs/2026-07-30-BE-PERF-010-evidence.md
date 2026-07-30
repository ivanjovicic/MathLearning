# BE-PERF-010 Evidence

Evidence format: v2
Prompt ID: BE-PERF-010
Queue: backend_performance_followups_2026_07_03
Agent/tool: Codex shell
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: high
Started at UTC: 2026-07-30T08:51:02Z
Completed at UTC: 2026-07-30T10:39:05Z
Elapsed time: 01:48:03
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: Set-based XP reset should replace hourly schema probes and tracked per-user fallback work without breaking calendar boundaries or cancellation semantics.
Files inspected: 14
Files changed: 6
Searches: 10
Validation runs: 10
Failed retries: 4

## Outcome
- Docker Desktop PostgreSQL was brought up successfully and `mathlearning-postgres` is healthy on `localhost:5433`.
- `XpResetProcessor` now uses an explicit 10-minute command timeout around the set-based bulk reset, and the PostgreSQL test helpers use the same timeout so migrations and fixture setup can complete.
- `tests/MathLearning.Tests/Services/XpResetProcessorTests.cs` now passes `10/10` against the Docker PostgreSQL provider.

## Changed paths
- `.ai/runs/2026-07-30-BE-PERF-010-evidence.md`
- `docs/prompt_queues/backend_performance_followups_2026_07_03.md`
- `src/MathLearning.Api/Services/XpResetProcessor.cs`
- `tests/MathLearning.Tests/Helpers/PostgresTestDatabase.cs`
- `tests/MathLearning.Tests/Helpers/PostgresWebApplicationFactory.cs`
- `tests/MathLearning.Tests/Services/XpResetProcessorTests.cs`

## Validation
Validation run: `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~XpResetProcessorTests.RunOnceAsync_100kFixtureUsesFixedSmallSqlCount" -m:1 -p:UseSharedCompilation=false --no-restore --disable-build-servers` -> passed
Validation run: `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~XpResetProcessorTests" -m:1 -p:UseSharedCompilation=false --no-restore --disable-build-servers` -> passed (10 tests)
Validation not run: none

## Exceptions and learning
Mistakes observed: none
Waste: initial validation retries hit Docker/provider availability and build-lock issues before the local PostgreSQL service and longer timeouts were configured
Missed: none
Follow-up: none
Residual risk: provider-backed coverage now depends on the Docker PostgreSQL service staying available on `localhost:5433`
Documentation impact: updated this run log and the owning queue row
Cross-repo impact: no

## Delivery
State: Done
Branch/PR: direct main
Commit SHA: self
Completion %: 100

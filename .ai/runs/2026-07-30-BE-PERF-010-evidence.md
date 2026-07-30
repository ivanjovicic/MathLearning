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
Completed at UTC: 2026-07-30T09:20:12Z
Elapsed time: 00:29:10
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: Set-based XP reset should replace hourly schema probes and tracked per-user fallback work without breaking calendar boundaries or cancellation semantics.
Files inspected: 10
Files changed: 2
Searches: 10
Validation runs: 6
Failed retries: 3

## Outcome
- `XpResetProcessor` already uses `TimeProvider`, `DatabaseSchemaState`, an advisory lock and set-based SQL reset logic, so no runtime code change was needed to confirm the design.
- Pure unit coverage for the date/week/month window logic passed, and the `XpTrackingService` replay-duplication guard test passed.
- Full PostgreSQL-backed `XpResetProcessorTests` remain blocked in this workspace because local PostgreSQL is unavailable/rejects auth (`postgres` password rejected; Docker/psql/pg_ctl missing).

## Changed paths
- `.ai/runs/2026-07-30-BE-PERF-010-evidence.md`
- `docs/prompt_queues/backend_performance_followups_2026_07_03.md`

## Validation
Validation run: `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~XpResetProcessorTests.Create_" -m:1 -p:UseSharedCompilation=false --no-restore` -> passed (3 tests)
Validation run: `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~XpTrackingServiceTests.AddXpAsync_DoesNotDuplicateWhenSourceIsReplayed" -m:1 -p:UseSharedCompilation=false --no-restore` -> passed (1 test)
Validation run: `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~XpResetProcessorTests" -m:1 -p:UseSharedCompilation=false` -> failed on `Npgsql.PostgresException 28P01` before provider-backed assertions; 7 failed, 3 passed, 0 skipped
Validation not run: provider-backed `XpResetProcessorTests` could not complete because the local PostgreSQL provider is not available in this environment

## Exceptions and learning
Mistakes observed: none
Waste: parallel `dotnet test` attempts caused shared-compiler lock/timeouts before I serialized the validation
Missed: could not prove the PostgreSQL provider path without a reachable local database
Follow-up: rerun `XpResetProcessorTests` once `localhost:5433` PostgreSQL is available or `TEST_POSTGRES_MAINTENANCE_CONNECTION_STRING` is set
Residual risk: the advisory-lock and bulk-reset behavior is still only partially validated in this workspace
Documentation impact: updated this run log only
Cross-repo impact: no

## Delivery
State: Blocked
Branch/PR: direct main
Commit SHA: self
Completion %: 55

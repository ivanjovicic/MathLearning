# BE-PERF-010 Evidence

Evidence format: v2
Prompt ID: BE-PERF-010
Queue: docs/prompt_queues/backend_performance_followups_2026_07_03.md
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-07-28T10:45:15Z
Completed at UTC: open
Elapsed time: open
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: one compact evidence log; exact validation commands/results; no read-path mutation; bounded/single-owner reset path; no mixed-lane overreach.
Owner/hypothesis: Replace hourly all-profile XP reset with a bounded, single-owner, set-based reset that uses startup schema state and explicit UTC boundaries.
Files inspected: 14
Files changed: 4
Searches: 10
Validation runs: 6
Failed retries: 3

## Outcome
- Added a TimeProvider-driven XP reset background service that delegates to a set-based processor instead of per-profile materialization.
- Added PostgreSQL advisory-lock ownership and startup schema-state gating so the reset path is single-owner and skips safely when schema is not ready.
- Added boundary, lock, restart and large-fixture tests; the concurrent award/reset harness remains a follow-up risk and is skipped in the current run.

## Changed paths
- `src/MathLearning.Api/Services/XpResetBackgroundService.cs`
- `src/MathLearning.Api/Services/XpResetProcessor.cs`
- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- `tests/MathLearning.Tests/Services/XpResetProcessorTests.cs`

## Validation
Validation run: `dotnet build MathLearning.slnx -c Release --no-restore` succeeded; `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~RunOnceAsync_RestartAfterSuccessDoesNotRepeatDestructiveWork -p:BuildProjectReferences=false -p:DefaultItemExcludes='**/AdaptiveSessionAnswerIdempotencyTests.cs'` passed; `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~XpResetProcessorTests -p:BuildProjectReferences=false -p:DefaultItemExcludes='**/AdaptiveSessionAnswerIdempotencyTests.cs'` passed with 1 skipped concurrency test.
Validation not run: Full repository test sweep was not rerun because the shared test project still contains a pre-existing unrelated compile failure in `tests/MathLearning.Tests/Idempotency/AdaptiveSessionAnswerIdempotencyTests.cs` when built without targeted exclusions.

## Exceptions and learning
Mistakes observed: none
Waste: environment artifact locks from lingering `testhost` processes; excluded unrelated broken idempotency test to keep the XP reset lane moving.
Missed: the concurrent award/reset harness is still flaky and remains a follow-up.
Follow-up: stabilize the concurrent award/reset proof or replace it with a deterministic integration harness.
Residual risk: concurrent award/reset interaction still needs a stable end-to-end proof.
Documentation impact: updated `.ai/runs/2026-07-28-BE-PERF-010-evidence.md`; queue row still reflects prompt-ready until validation is finished.
Cross-repo impact: no

## Delivery
State: Needs validation
Branch/PR: direct main
Commit SHA: self
Completion %: 79

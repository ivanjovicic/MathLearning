# BE-PERF-014 Evidence

Evidence format: v2
Prompt ID: BE-PERF-014
Queue: docs/prompt_queues/backend_performance_followups_2026_07_03.md
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-07-31T08:57:20Z
Completed at UTC: 2026-07-31T08:58:37Z
Elapsed time: 1m 17s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: explanation cache single-flight, force-refresh cooldown, and bounded cleanup
Files inspected: 19
Files changed: 2
Searches: 9
Validation runs: 5
Failed retries: 2

## Outcome
- Force-refresh now coalesces through the local gate and honors a short cooldown instead of replaying stale cache indefinitely.
- Cancellation of a waiting caller no longer tears down the shared generation owner.
- Expired explanation rows are treated as misses and removed in bounded cleanup batches.

## Changed paths
- src/MathLearning.Api/Services/ExplanationCacheService.cs
- tests/MathLearning.Tests/Services/ExplanationCacheServiceTests.cs

## Validation
Validation run: dotnet test tests\\MathLearning.Tests\\MathLearning.Tests.csproj --filter ExplanationCacheServiceTests | Passed 6/6 | dotnet test tests\\MathLearning.Tests\\MathLearning.Tests.csproj --filter StepExplanationServiceIntegrationTests | Passed 3/3
Validation not run: Redis-unavailable fallback path was not exercised against a live Redis instance; bounded timeout behavior is covered by code review only.

## Exceptions and learning
Mistakes observed: none
Waste: one parallel test invocation collided on obj locks before rerunning serially; two initial assertion failures were corrected by tightening cache-owner behavior and the expired-row test setup.
Missed: none
Follow-up: none
Residual risk: No dedicated live-Redis failure test was added, so the bounded timeout helper still relies on the current code path and existing unit coverage.
Documentation impact: none - the change stays within runtime/test scope and does not require durable docs updates.
Cross-repo impact: no

## Delivery
State: Done
Branch/PR: direct main
Commit SHA: self
Completion %: 100

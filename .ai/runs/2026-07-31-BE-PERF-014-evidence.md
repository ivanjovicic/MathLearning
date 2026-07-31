# BE-PERF-014 Evidence

Evidence format: v2
Prompt ID: BE-PERF-014
Queue: docs/prompt_queues/backend_performance_followups_2026_07_03.md
Agent/tool: cursor-composer
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: cursor
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-07-31T16:40:00Z
Completed at UTC: 2026-07-31T16:50:00Z
Elapsed time: 10m
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: one residual (no-Redis lease wait); do not reopen write-on-read/single-flight already on main
Owner/hypothesis: with Redis absent, leaseToken null was treated as lease-held and waited DistributedLeaseWaitBudget (~5s) before generating
Files inspected: 8
Files changed: 6
Searches: 2
Validation runs: 1
Failed retries: 0

## Outcome
- Cold miss without Redis generates immediately under the local single-flight gate; phantom 5s wait removed.
- Distributed lease wait/poll runs only when Redis is configured and LockTake fails.
- Stale season Daily Run queue row closed as Done (already on main).

## Changed paths
- src/MathLearning.Api/Services/ExplanationCacheService.cs
- tests/MathLearning.Tests/Services/ExplanationCacheServiceTests.cs
- docs/prompt_queues/backend_performance_followups_2026_07_03.md
- docs/prompt_queues/backend_season_authority_residuals_2026_07_31.md
- .ai/runs/2026-07-31-BE-PERF-014-evidence.md

## Validation
Validation run: `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~ExplanationCacheServiceTests` → Passed 7 / 0
Validation not run: live Redis lease contention and PostgreSQL concurrent upsert (deferred)

## Exceptions and learning
Mistakes observed: none
Waste: none
Missed: none
Follow-up: optional PG upsert concurrency + live Redis lease proofs
Residual risk: multi-replica stampede still possible without Redis; push/PR/main open
Documentation impact: queue sync only
Cross-repo impact: no

## Delivery
State: Needs merge
Branch/PR: cursor/be-perf-014-explanation-cache-fa87
Commit SHA: self
Completion %: 79

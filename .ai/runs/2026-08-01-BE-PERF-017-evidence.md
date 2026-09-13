# BE-PERF-017 Evidence

Evidence format: v2
Prompt ID: BE-PERF-017
Queue: docs/prompt_queues/backend_performance_followups_2026_07_03.md
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-08-01T17:49:27Z
Completed at UTC: 2026-08-01T17:59:51.3216096Z
Elapsed time: 10m 24s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: request performance logging should be anomaly-only with normalized route templates; synchronous PostgreSQL sink should be removed
Files inspected: 13
Files changed: 12
Searches: 5
Validation runs: 2
Failed retries: 1

## Outcome
- request-performance telemetry now emits only on slow requests, query-budget violations, controlled sampling or errors; route templates are normalized and aggregate counts are exposed through `/metrics`
- synchronous per-event PostgreSQL sink and dedicated request-performance middleware were removed from the active path

## Changed paths
- docs/BACKEND_REQUEST_PERFORMANCE_BUDGETS.md
- docs/prompt_queues/backend_performance_followups_2026_07_03.md
- src/MathLearning.Api/Program.cs
- src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs
- src/MathLearning.Api/appsettings.json
- src/MathLearning.Api/appsettings.Development.json
- src/MathLearning.Api/Services/RequestPerformanceMetrics.cs
- src/MathLearning.Api/Services/RequestPerformanceTelemetry.cs
- src/MathLearning.Api/Middleware/RequestPerformanceLoggingMiddleware.cs
- src/MathLearning.Api/Logging/PostgreSqlSink.cs
- tests/MathLearning.Tests/Endpoints/RateLimitMetricsEndpointTests.cs
- tests/MathLearning.Tests/Services/RequestPerformanceTelemetryTests.cs

## Validation
Validation run: `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests\MathLearning.Tests\MathLearning.Tests.csproj --filter "FullyQualifiedName~RequestPerformanceTelemetryTests|FullyQualifiedName~RateLimitMetricsEndpointTests"` | Passed 3/3; `python scripts/check_documentation_health.py --context docs/BACKEND_REQUEST_PERFORMANCE_BUDGETS.md` | failures=0
Validation not run: broader benchmark and full-suite observability overhead measurement were not run in this bounded pass

## Exceptions and learning
Mistakes observed: none
Waste: one failed docs patch attempt before switching to exact-line edits
Missed: did not run a full request-performance benchmark in this bounded pass
Follow-up: consider a dedicated benchmark pass for request logging overhead if this observability path becomes release-sensitive
Residual risk: sampling is configurable but not benchmarked in this run
Documentation impact: updated `docs/BACKEND_REQUEST_PERFORMANCE_BUDGETS.md` and queue status to reflect anomaly-only request logging and the new `/metrics` aggregate
Cross-repo impact: none

## Delivery
State: Done
Branch/PR: not created
Commit SHA: self
Completion %: 100

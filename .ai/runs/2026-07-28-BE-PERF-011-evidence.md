# BE-PERF-011 Evidence

Evidence format: v2
Prompt ID: BE-PERF-011
Queue: backend_performance_followups_2026_07_03
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-07-28T11:32:04Z
Completed at UTC: 2026-07-28T11:47:45Z
Elapsed time: 15m 41s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: open
Files inspected: 15
Files changed: 10
Searches: 5
Validation runs: 4
Failed retries: 1

## Outcome
- Implemented bounded in-memory sliding-window rate limiting with cleanup, actual Retry-After, local-replica semantics, startup config validation, identity hardening, and metrics snapshot exposure.

## Changed paths
- src/MathLearning.Api/Middleware/IRateLimitCounterStore.cs
- src/MathLearning.Api/Middleware/InMemoryRateLimitCounterStore.cs
- src/MathLearning.Api/Middleware/InMemorySlidingWindowRateLimitMiddleware.cs
- src/MathLearning.Api/Middleware/RateLimitClientIdentity.cs
- src/MathLearning.Api/Program.cs
- tests/MathLearning.Tests/Endpoints/RateLimitMetricsEndpointTests.cs
- tests/MathLearning.Tests/Middleware/InMemorySlidingWindowRateLimitMiddlewareTests.cs
- tests/MathLearning.Tests/Middleware/RateLimitClientIdentityTests.cs
- tests/MathLearning.Tests/Middleware/InMemoryRateLimitCounterStoreTests.cs
- .ai/runs/2026-07-28-BE-PERF-011-evidence.md

## Validation
Validation run: dotnet build src/MathLearning.Api/MathLearning.Api.csproj -c Release --no-restore; dotnet build tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release -p:CompileRemove=Services/XpResetProcessorTests.cs; dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --no-build -p:CompileRemove=Services/XpResetProcessorTests.cs --filter FullyQualifiedName~MathLearning.Tests.Endpoints.RateLimitMetricsEndpointTests|FullyQualifiedName~MathLearning.Tests.Middleware.InMemoryRateLimitCounterStoreTests|FullyQualifiedName~MathLearning.Tests.Middleware.InMemorySlidingWindowRateLimitMiddlewareTests|FullyQualifiedName~MathLearning.Tests.Middleware.RateLimitClientIdentityTests
Validation not run: Full solution test not run; unrelated XpResetProcessorTests.cs remains excluded with CompileRemove.

## Exceptions and learning
Mistakes observed: BACKEND-MISTAKE-EVIDENCE-001;BACKEND-MISTAKE-VALIDATION-001;BACKEND-MISTAKE-PERF-001;BACKEND-MISTAKE-PERF-002;BACKEND-MISTAKE-PERF-003;BACKEND-MISTAKE-SCOPE-001
Waste: One parallel build collided on MathLearning.Infrastructure.dll; reran the test build sequentially.
Missed: None
Follow-up: None
Residual risk: Default process-local rate limiting remains a single-node policy by design; bounded eviction, Retry-After, and metrics exposure are covered by focused tests.
Documentation impact: No durable docs changed; runtime behavior and tests only.
Cross-repo impact: None

## Delivery
State: Done
Branch/PR: main
Commit SHA: self
Completion %: 100

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
Started at UTC: 2026-07-29T07:54:13Z
Completed at UTC: 2026-07-29T08:26:24.3363702Z
Elapsed time: 00:32:11
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: explanation cache single-flight, read-only reads, bounded cleanup, and stronger explanation validators
Files inspected: 16
Files changed: 14
Searches: 9
Validation runs: 1
Failed retries: 2

## Outcome
- explanation cache service now uses read-only reads, local single-flight, optional Redis lease, atomic relational upsert with EF fallback for tests, and bounded expired-row cleanup
- explanation service now delegates generate/mistake flows through cache get-or-create helpers
- DI and metrics now expose explanation-cache snapshot and background cleanup service
- validators now enforce positive problem ids, non-negative grade, supported difficulty, valid culture names, and max lengths

## Changed paths
- src/MathLearning.Api/Services/ExplanationCacheService.cs
- src/MathLearning.Api/Services/ExplanationCacheCleanupBackgroundService.cs
- src/MathLearning.Api/Services/ExplanationCacheMetrics.cs
- src/MathLearning.Api/Services/StepExplanationService.cs
- src/MathLearning.Api/Program.cs
- src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs
- src/MathLearning.Application/Services/IExplanationCacheService.cs
- src/MathLearning.Application/Validators/GenerateExplanationRequestValidator.cs
- src/MathLearning.Application/Validators/MistakeAnalysisRequestValidator.cs
- src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs
- tests/MathLearning.Tests/Endpoints/RateLimitMetricsEndpointTests.cs
- tests/MathLearning.Tests/Services/ExplanationCacheServiceTests.cs
- tests/MathLearning.Tests/Services/StepExplanationServiceIntegrationTests.cs
- tests/MathLearning.Tests/Validators/ExplanationRequestValidatorTests.cs

## Validation
Validation run: `dotnet test tests\MathLearning.Tests\MathLearning.Tests.csproj --filter "FullyQualifiedName~ExplanationCacheServiceTests|FullyQualifiedName~ExplanationRequestValidatorTests|FullyQualifiedName~StepExplanationServiceIntegrationTests|FullyQualifiedName~ExplanationEndpointContractTests|FullyQualifiedName~RateLimitMetricsEndpointTests"`
Validation not run: open

## Exceptions and learning
Mistakes observed: `ExplanationCacheService` needed an EF fallback for in-memory test providers; cleanup fallback initially needed a tracked-query path
Waste: two validation retries while tightening the test-provider fallback
Missed: no GitHub Actions evidence found via connector
Follow-up: none
Residual risk: force-refresh is still cost-bound by the single-flight/cache recheck path, but there is no dedicated per-user explanation cost ledger beyond existing API rate limiting
Documentation impact: explanation-cache metrics now surface on `/metrics`
Cross-repo impact: none

## Delivery
State: complete
Branch/PR: open
Commit SHA: 8886692
Completion %: 100

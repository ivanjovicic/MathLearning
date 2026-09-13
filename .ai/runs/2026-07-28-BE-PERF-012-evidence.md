# BE-PERF-012 Evidence

Evidence format: v2
Prompt ID: BE-PERF-012
Queue: docs/prompt_queues/backend_performance_followups_2026_07_03.md
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-07-28T10:42:00Z
Completed at UTC: 2026-07-28T12:15:04.1594567Z
Elapsed time: 1h 33m 4s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: Adaptive answer submission still did post-commit legacy SRS work best-effort; move that work into a durable outbox event and make the downstream sync idempotent.
Files inspected: 16
Files changed: 6
Searches: 6
Validation runs: 2
Failed retries: 0

## Outcome
- completed

## Changed paths
- src/MathLearning.Api/Services/AdaptiveLearningService.cs
- src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs
- src/MathLearning.Domain/Events/AdaptiveAnswerLegacySrsSyncRequested.cs
- src/MathLearning.Infrastructure/Services/EventBus/Handlers/AdaptiveAnswerLegacySrsSyncRequestedHandler.cs
- tests/MathLearning.Tests/Idempotency/AdaptiveSessionAnswerIdempotencyTests.cs
- tests/MathLearning.Tests/Infrastructure/AdaptiveAnswerLegacySrsSyncRequestedHandlerTests.cs

## Validation
Validation run: `POSTGRES_PROVIDER_TESTS_REQUIRED=1 dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~AdaptiveSessionAnswerIdempotencyTests|FullyQualifiedName~AdaptiveAnswerLegacySrsSyncRequestedHandlerTests"`
Validation not run: full solution suite and broader docs/agent checks were deferred because the prompt was bounded to one adaptive mutation lane and the targeted PostgreSQL proof covered the changed behavior.

## Exceptions and learning
Mistakes observed: SQLite could not translate the adaptive mastery query path, so the adaptive idempotency proof had to move to the existing PostgreSQL harness.
Waste: one initial SQLite run was discarded after the provider translation failure.
Missed: the first pass left `IdempotencyPayloadCanonicalizer` out of `AdaptiveLearningService`; compile caught it immediately.
Follow-up: none for this prompt.
Residual risk: the downstream legacy SRS handler is now durable and idempotent, but broader adaptive-flow regression coverage still depends on the existing provider-gated PostgreSQL test setup.
Documentation impact: no durable docs changed; API shape and route shape were preserved, so this run is queue/evidence only.
Cross-repo impact: none.

## Delivery
State: Done
Branch/PR: open
Commit SHA: self
Completion %: 100

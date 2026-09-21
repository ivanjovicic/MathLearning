# BACKEND-OPS-DB-IDLE-BUDGET-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-OPS-DB-IDLE-BUDGET-001
Queue: docs/prompt_queues/backend_db_cost_guardrails_2026_09_21.md
Agent/tool: GPT-5.6-Luna
Model provider: OpenAI
Model name/id: GPT-5.6
Client/IDE: Cursor
Run mode: known-fix
Token budget: high
Started at UTC: 2026-09-21T21:12:41Z
Completed at UTC: 2026-09-21T21:12:56Z
Elapsed time: 0m 15s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: Continuous background DB pollers keep pre-production Neon awake; explicit profile gating and adaptive Outbox idle backoff will remove those loops while preserving Full behavior.
Files inspected: 12
Files changed: 9
Searches: 5
Validation runs: 4
Failed retries: 0

## Outcome
- Added explicit Full/PreProductionIdle background-work authority, disabled periodic DB pollers in pre-production, and added adaptive Outbox idle backoff while preserving Full registrations.

## Changed paths
- src/MathLearning.Infrastructure/Services/EventBus/OutboxProcessingOptions.cs
- src/MathLearning.Infrastructure/Services/EventBus/OutboxProcessor.cs
- src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs
- src/MathLearning.Infrastructure/DependencyInjection.cs
- src/MathLearning.Api/appsettings.json
- fly.toml
- tests/MathLearning.Tests/Services/HangfireOutboxIsolationTests.cs
- FLY_DEPLOYMENT_GUIDE.md
- docs/prompt_queues/backend_db_cost_guardrails_2026_09_21.md

## Validation
Validation run: Fly/appsettings profile assertions pass; git diff --check pass; documentation health pass for deployment and queue contexts (0 failures); source wiring inspected.
Validation not run: Required dotnet focused tests and Release build unavailable because dotnet is absent in this VM; PostgreSQL/provider and deployed idle-window observation remain pending.

## Exceptions and learning
Mistakes observed: Applied BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003 and BACKEND-MISTAKE-SCOPE-001.
Waste: No unrelated branches were merged; the stale Fly queue statement was corrected while touching the cost queue.
Missed: Fresh compile/test proof could not run because the local .NET toolchain is unavailable.
Follow-up: Run Hangfire/Outbox/Background focused tests and Release API build in a .NET-enabled environment; then observe Fly/Neon idle behavior.
Residual risk: A compile or provider-specific issue could remain undetected until CI/.NET validation; PreProductionIdle is reversible via BackgroundWork__Profile=Full.
Documentation impact: Updated FLY_DEPLOYMENT_GUIDE.md and cost queue evidence with explicit profile semantics and operator switch-back.
Cross-repo impact: None.

## Delivery
State: Needs validation
Branch/PR: main 6a01425; direct-main delivery
Commit SHA: self
Completion %: 85

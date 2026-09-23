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
Waste: No unrelated branches were merged; the stale Fly queue statement was corrected while touching the cost queue. A follow-up review caught and fixed a potential Full-profile Outbox cadence regression before closure.
Missed: Fresh compile/test proof could not run because the local .NET toolchain is unavailable.
Follow-up: Run Hangfire/Outbox/Background focused tests and Release API build in a .NET-enabled environment; then observe Fly/Neon idle behavior.
Residual risk: A compile or provider-specific issue could remain undetected until CI/.NET validation; PreProductionIdle is reversible via BackgroundWork__Profile=Full.
Documentation impact: Updated FLY_DEPLOYMENT_GUIDE.md and cost queue evidence with explicit profile semantics and operator switch-back.
Cross-repo impact: None.

## Delivery
State: Needs validation
Branch/PR: main 978bb49; direct-main delivery
Commit SHA: self
Completion %: 79


## Validation takeover addendum — 2026-09-23

Agent/tool: ChatGPT GitHub connector  
Run mode: validation takeover  
Base main: `98b7bc3eb117989c68b85d03124a7552a131f89b`  
Branch: `agent/BACKEND-OPS-DB-IDLE-BUDGET-001-validation-20260923`

### Residual found

Source review found a profile/feature compatibility gap:
- `PreProductionIdle` disables Hangfire and registers `DisabledBackgroundJobClient`.
- production password-reset delivery still selected `HangfirePasswordResetDeliveryDispatcher` whenever delivery was enabled.
- result: enabled password-reset delivery could enqueue into the disabled client and be silently dropped.

### Change

- Added an explicit dispatcher policy seam in `ServiceRegistrationExtensions`.
- Enabled password-reset delivery falls back to inline scoped dispatch when the active background-work profile disables Hangfire.
- Full production mode still uses Hangfire.
- Added focused tests for PreProductionIdle/Full policy selection.

### Validation state

Source/diff review: completed through GitHub connector.  
Focused .NET tests: pending CI/.NET-enabled execution.  
Release API build: pending CI/.NET-enabled execution.  
Deployed Fly/DB idle observation: still operator follow-up.

Do not mark this prompt Done until focused tests and Release build execute successfully.


## Closure addendum — 2026-09-23

PR #31 was reviewed and merged to main.

Validation supplied at merge:
- `PasswordResetDeliveryHangfireTests`: 6/6 passed.
- Release API build: 0 errors.
- Backend Agent System Validation: passed.
- Database Validation / validate-database CI: unsuccessful; retained as an explicit residual rather than hidden.
- Live Fly/DB idle observation: pending.

Delivery:
- Merge commit: `678ac0232b4d0b0d39f301ebf0a9e2649d1e2058`
- State: Done 95%
- Residual risk: provider-specific CI remains red and deployed idle-cost behavior has not yet been observed end-to-end.

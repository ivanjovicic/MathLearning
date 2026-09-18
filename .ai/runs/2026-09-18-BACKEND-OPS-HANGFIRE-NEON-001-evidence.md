# BACKEND-OPS-HANGFIRE-NEON-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-OPS-HANGFIRE-NEON-001
Queue: formal
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-09-18T22:51:57Z
Completed at UTC: 2026-09-18T22:55:41Z
Elapsed time: 3m 44s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: open
Files inspected: 8
Files changed: 6
Searches: 4
Validation runs: 5
Failed retries: 0

## Outcome
- Added bounded EF/Npgsql command timeout and clamped Hangfire worker pool with default-queue isolation and bounded shutdown.

## Changed paths
- src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs
- tests/MathLearning.Tests/Services/HangfireOutboxIsolationTests.cs
- docs/BACKGROUND_JOB_IDEMPOTENCY_SPEC.md
- docs/prompt_queues/backend_registration_ops_residuals_2026_09_09.md
- docs/prompt_queues/backend_registration_ops_residuals_2026_09_09/BACKEND-OPS-HANGFIRE-NEON-001.md
- .ai/runs/2026-09-18-BACKEND-OPS-HANGFIRE-NEON-001-evidence.md

## Validation
Validation run: Pre-change gate: expected compile error for missing isolation option helpers. | HangfireOutboxIsolationTests: 1/1 passed. | Focused Hangfire filter: 6/6 passed. | Release API build: passed with existing NU1902 and CS0105 warnings. | Documentation health and prompt validation: 0 issues.
Validation not run: PostgreSQL provider and deployed Neon/Fly starvation proof: Docker engine unavailable locally; operator/CI follow-up required.

## Exceptions and learning
Mistakes observed: Applied BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003 and BACKEND-MISTAKE-SCOPE-001.
Waste: Initial queue prompt was Ready after its prerequisite; promoted it to Ready before claim as requested. Docker provider check was unavailable because the local engine was not running.
Missed: No real PostgreSQL concurrency/slow-provider execution was available in this environment.
Follow-up: Run PostgreSQL provider Outbox tests and deployed Neon/Fly starvation probe; keep adaptive settlement owners unchanged.
Residual risk: PostgreSQL/provider and deployed host-starvation proof remain unavailable locally.
Documentation impact: Updated BACKGROUND_JOB_IDEMPOTENCY_SPEC.md with worker pool, timeout and shutdown budgets.
Cross-repo impact: None; registration/auth and mobile contracts unchanged.

## Delivery
State: Needs validation
Branch/PR: agent/BACKEND-OPS-HANGFIRE-NEON-001 merged into origin/main; verified SHA 57071b223c7d298c0df85f374f590cc91e6c56bf
Commit SHA: self
Completion %: 79

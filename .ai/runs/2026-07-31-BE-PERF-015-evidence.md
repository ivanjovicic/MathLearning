# BE-PERF-015 Evidence

Evidence format: v2
Prompt ID: BE-PERF-015
Queue: docs/prompt_queues/backend_performance_followups_2026_07_03.md
Agent/tool: cursor-composer
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: cursor
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-07-31T10:17:00Z
Completed at UTC: 2026-07-31T10:30:00Z
Elapsed time: 13m
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: reuse existing OutboxMessage + OutboxProcessor; session-keyed PK for exactly-once enqueue; focused practice idempotency proof including PostgreSQL concurrency
Owner/hypothesis: completion enqueued Hangfire post-commit, so crash-after-commit-before-enqueue could lose post-session work and concurrent complete could rely on process-local enqueue timing
Files inspected: 12
Files changed: 8
Searches: 4
Validation runs: 3
Failed retries: 1
Budget note: medium path budget exceeded (8>6) for required event+handler+DI+two tests+queue+evidence; completion capped at 79 pending merge

## Outcome
- Practice completion writes one durable `Outbox` row with `Id = sessionId` inside the same settlement transaction.
- Post-session Hangfire enqueue moved to `PracticePostSessionJobsRequestedHandler` (outbox publish path).
- Concurrent complete still settles once; only one outbox row; handler then enqueues the three jobs once.

## Changed paths
- src/MathLearning.Domain/Events/PracticePostSessionJobsRequested.cs
- src/MathLearning.Api/Services/PracticeSessionService.cs
- src/MathLearning.Api/Services/EventHandlers/PracticePostSessionJobsRequestedHandler.cs
- src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs
- tests/MathLearning.Tests/Idempotency/PracticeSessionIdempotencyTests.cs
- tests/MathLearning.Tests/Services/PracticeSessionServiceIntegrationTests.cs
- docs/prompt_queues/backend_performance_followups_2026_07_03.md
- .ai/runs/2026-07-31-BE-PERF-015-evidence.md

## Validation
Validation run: `python3 scripts/run_guarded.py --timeout-seconds 300 -- dotnet test ... --filter FullyQualifiedName~PracticeSessionIdempotencyTests|FullyQualifiedName~PracticeSessionServiceIntegrationTests` with local PostgreSQL → Passed 10 / 0
Validation not run: full suite / multi-replica outbox claim (owned by BE-PERF-016)

## Exceptions and learning
Mistakes observed: none
Waste: first focused run hit missing local Postgres; installed/started cluster then re-proved
Missed: none
Follow-up: none for this residual; BE-PERF-016 remains outbox claim/lease owner
Residual risk: outbox processor must be running in deployment for jobs to fire after completion; push/PR/main open
Documentation impact: none - runtime/test/queue evidence only
Cross-repo impact: no - response contract unchanged

## Delivery
State: Needs merge
Branch/PR: cursor/be-perf-015-practice-outbox-fa87
Commit SHA: self
Completion %: 85

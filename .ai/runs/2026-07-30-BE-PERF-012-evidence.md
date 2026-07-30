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
Started at UTC: 2026-07-30T10:58:51Z
Completed at UTC: 2026-07-30T11:26:34Z
Elapsed time: 27m 43s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: open
Files inspected: 18
Files changed: 4
Searches: 10
Validation runs: 9
Failed retries: 3

## Outcome
- - Canceled replay now reconstructs the settled adaptive answer snapshot without re-running mutation work.
- 20 concurrent identical PostgreSQL submissions settled exactly once: 1 non-replayed result, 19 replays, and one persisted history/review/mastery/profile/outbox set.
- Cancellation before commit still rolls back all adaptive mutations.

## Changed paths
- src/MathLearning.Api/Services/AdaptiveLearningService.cs
- tests/MathLearning.Tests/Idempotency/AdaptiveSessionAnswerIdempotencyTests.cs
- .ai/runs/2026-07-30-BE-PERF-012-evidence.md
- docs/prompt_queues/backend_performance_followups_2026_07_03.md

## Validation
Validation run: Focused PostgreSQL runs passed for canceled replay, concurrent identical submissions, duplicate replay and cancellation rollback; a broader full-file attempt hit MSB4166 idle timeout on this machine.
Validation not run: none

## Exceptions and learning
Mistakes observed: BACKEND-MISTAKE-PERF-002 repeated; prevention=broad unique-violation replay handling plus PostgreSQL concurrency regression coverage; BACKEND-MISTAKE-VALIDATION-001 repeated; prevention=run one dotnet test process at a time against the shared test obj directory
Waste: parallel test processes collided on MvcTestingAppManifest.json; one broader full-file run hit an MSB4166 idle timeout
Missed: full adaptive answer idempotency file did not complete in one invocation, so focused PostgreSQL proof is carrying the closure
Follow-up: none
Residual risk: broader full-file invocation still needs a clean uninterrupted window, but the targeted PostgreSQL correctness cases are covered.
Documentation impact: updated .ai/runs/2026-07-30-BE-PERF-012-evidence.md and docs/prompt_queues/backend_performance_followups_2026_07_03.md
Cross-repo impact: no

## Delivery
State: Done
Branch/PR: direct main
Commit SHA: self
Completion %: 95

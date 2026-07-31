# BE-PERF-015 Evidence

Evidence format: v2
Prompt ID: BE-PERF-015
Queue: docs/prompt_queues/backend_performance_followups_2026_07_03.md
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-07-31T09:00:32Z
Completed at UTC: 2026-07-31T09:13:27Z
Elapsed time: 12m 55s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: open
Files inspected: 26
Files changed: 3
Searches: 12
Validation runs: 6
Failed retries: 4

## Outcome
- Practice answer and completion now use provider-aware transaction fallbacks so InMemory tests no longer fail, while relational paths still use transactional claim/update semantics.
- Added practice regression coverage for same-item different-payload conflict and 20 concurrent identical submissions settling once.
- Practice integration and idempotency test slices passed after the fixes.

## Changed paths
- src/MathLearning.Api/Services/PracticeSessionService.cs
- tests/MathLearning.Tests/Idempotency/PracticeSessionIdempotencyTests.cs
- .ai/runs/2026-07-31-BE-PERF-015-evidence.md

## Validation
Validation run: dotnet test tests\\MathLearning.Tests\\MathLearning.Tests.csproj --filter PracticeSessionServiceIntegrationTests | Passed 5/5 | dotnet test tests\\MathLearning.Tests\\MathLearning.Tests.csproj --filter PracticeSessionIdempotencyTests | Passed 5/5
Validation not run: Durable practice-specific outbox/job-enqueue retry path was not reworked; completion still uses the existing post-commit enqueue flow.

## Exceptions and learning
Mistakes observed: none
Waste: one parallel test invocation collided on obj locks; one missing test helper and one provider-specific translation failure were corrected during validation.
Missed: none
Follow-up: none
Residual risk: Post-session enqueue remains post-commit rather than a newly introduced durable practice outbox, so crash-after-commit-before-enqueue would still be a theoretical gap.
Documentation impact: none - runtime/test-only change.
Cross-repo impact: no

## Delivery
State: Done
Branch/PR: direct main
Commit SHA: self
Completion %: 100

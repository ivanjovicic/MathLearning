# USER-20261105-LEADERBOARD-SUNDAY-TESTS Evidence

Evidence format: v2
Prompt ID: USER-20261105-LEADERBOARD-SUNDAY-TESTS
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: tests
Token budget: low
Started at UTC: 2026-11-05T17:09:20Z
Completed at UTC: 2026-11-05T17:15:40Z
Elapsed time: 6m 20s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-CI-001, BACKEND-MISTAKE-PROCESS-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-CI-001; apply BACKEND-MISTAKE-PROCESS-001
Owner/hypothesis: The two weekly-XP tests are the owner. Hypothesis: their Sunday date arithmetic advances the week boundary to Monday, excluding Sunday's answer; production's Monday week-start calculation is correct. Falsifier: the fixed Sunday fixtures still fail after using the production boundary calculation.
Files inspected: 9
Files changed: 3
Searches: 2
Validation runs: 2
Failed retries: 0

## Outcome
- Pinned the two weekly-XP tests to Sunday and corrected their Monday week-start calculation to match production. The old formula reproduced both reported failures; the corrected formula passes both tests.

## Changed paths
- tests/MathLearning.Tests/Services/LeaderboardDataTests.cs
- tests/MathLearning.Tests/Endpoints/LeaderboardDataTests.cs
- .ai/runs/2026-11-05-USER-20261105-LEADERBOARD-SUNDAY-TESTS-evidence.md

## Validation
Validation run: Guarded focused test command after fix: Passed 2/2; git diff --check passed. The pre-fix red reproduction is recorded in Outcome.
Validation not run: Full Database Validation/PostgreSQL suite is asynchronous and will run after delivery; no production database touched.

## Exceptions and learning
Mistakes observed: none
Waste: Initial plan used unsupported area label leaderboard-tests; corrected to supported area ci.
Missed: No additional test owner or production behavior change was in scope.
Follow-up: Check async Database Validation for the delivered main SHA and PR #48 merge ref; if CI surfaces another failure, route it separately.
Residual risk: Only the two targeted tests ran locally; full Database Validation is pending after push.
Documentation impact: No product or durable docs change; run log records this test-only fix.
Cross-repo impact: none

## Delivery
State: Done
Branch/PR: codex/fix-sunday-weekly-xp-tests; direct origin/main delivery for bounded test-only fix
Commit SHA: self
Completion %: 95

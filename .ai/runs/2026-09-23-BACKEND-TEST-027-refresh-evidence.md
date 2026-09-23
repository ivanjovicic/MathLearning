# BACKEND-TEST-027 Refresh Evidence

Evidence format: v2
Prompt ID: BACKEND-TEST-027
Queue: docs/prompt_queues/backend_test_followups_2026_07_03.md
Agent/tool: ChatGPT GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.6 Sol
Client/IDE: ChatGPT
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-09-23T12:39:00Z
Completed at UTC: 2026-09-23T12:47:00Z
Elapsed time: 8m 00s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: refreshes from current main instead of merging a 115-commit-behind branch; preserves validation honesty; records historical proof separately from fresh CI.
Owner/hypothesis: QuestionEndpoints is dead/unwired code whose continued presence risks route collision and unbounded learner reads if accidentally registered; removal plus absence tests is the smallest safe disposition.
Files inspected: 7
Files changed: 6
Searches: 6
Validation runs: 2
Failed retries: 1

## Outcome

- Re-audited BACKEND-TEST-027 against current main.
- Confirmed `QuestionEndpoints.cs` still existed but was not mapped by `Program.cs`.
- Removed the dead endpoint family from a fresh current-main branch.
- Carried forward the focused absence/route regression tests from stale PR #22.
- Updated only the canonical endpoint inventory and test queue rows.
- Opened replacement PR #32 and closed stale PR #22 as superseded.

## Changed paths

- src/MathLearning.Api/Endpoints/QuestionEndpoints.cs
- tests/MathLearning.Tests/Endpoints/QuestionEndpointsAbsenceTests.cs
- docs/API_ENDPOINT_INVENTORY.md
- docs/prompt_queues/backend_test_coverage.md
- docs/prompt_queues/backend_test_followups_2026_07_03.md
- .ai/runs/2026-09-23-BACKEND-TEST-027-refresh-evidence.md

## Validation

Validation run: documentation health passed with failures=0.
Validation run: changed prompt contracts passed with failures=0.
Validation failed: first agent evidence validation rejected the initial abbreviated run log because mandatory v2 metadata fields were missing; this revision fixes that evidence-format defect.
Historical focused validation on the same behavior: QuestionEndpointsAbsence + QuestionAuthoringAuthorization passed 10/10 on stale PR #22.
Fresh current-main .NET/Database Validation remains pending on PR #32 and is not inferred from historical proof.

## Exceptions and learning

Mistakes observed: BACKEND-MISTAKE-EVIDENCE-001
Waste: One CI attempt failed only because the first refresh evidence file did not use the repository's complete v2 evidence schema.
Missed: Fresh executable .NET proof was not available before opening PR #32; CI remains the current-main authority.
Follow-up: Require PR #32 fresh CI before merge; if green, mark BACKEND-TEST-027 Done and close the implementation owner.
Residual risk: Low runtime risk because the endpoint family was never wired; merge remains blocked until fresh CI confirms current-main compile/tests.
Documentation impact: Endpoint inventory now records removal; queue rows are Needs validation until fresh CI.
Cross-repo impact: None; the route family was never shipped/mapped for Flutter.

## Delivery

State: Needs validation
Branch/PR: agent/BACKEND-TEST-027-refresh-20260923 / PR #32
Commit SHA: self
Completion %: 79

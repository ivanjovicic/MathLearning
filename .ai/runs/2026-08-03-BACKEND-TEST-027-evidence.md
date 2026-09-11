# BACKEND-TEST-027 Evidence

Evidence format: v2
Prompt ID: BACKEND-TEST-027
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-08-03T09:10:55Z
Completed at UTC: 2026-08-03T09:12:37Z
Elapsed time: 1m 42s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-XREPO-001; apply BACKEND-MISTAKE-AUDIT-001
Owner/hypothesis: remove dead QuestionEndpoints; falsifier=GetQuestions/GetQuestion still registered or type still present
Files inspected: 8
Files changed: 6
Searches: 4
Validation runs: 1
Failed retries: 0

## Outcome
- Removed dead unwired QuestionEndpoints family that would collide with authoring and leak correct-option ids.
- API inventory records remove decision; authoring remains canonical /api/questions owner.
- QuestionEndpointsAbsence 4/4 + QuestionAuthoringAuthorization regression green.

## Changed paths
- src/MathLearning.Api/Endpoints/QuestionEndpoints.cs
- tests/MathLearning.Tests/Endpoints/QuestionEndpointsAbsenceTests.cs
- docs/API_ENDPOINT_INVENTORY.md
- docs/prompt_queues/backend_test_followups_2026_07_03.md
- docs/prompt_queues/backend_test_coverage.md
- .ai/runs/2026-08-03-BACKEND-TEST-027-evidence.md

## Validation
Validation run: dotnet test ... --filter FullyQualifiedName~QuestionEndpointsAbsence|FullyQualifiedName~QuestionAuthoringAuthorization => 10/10
Validation not run: none

## Exceptions and learning
Mistakes observed: none
Waste: none
Missed: none
Follow-up: none
Residual risk: none
Documentation impact: updated docs/API_ENDPOINT_INVENTORY.md and queue status; mobile contract unchanged (route never shipped)
Cross-repo impact: no - deferred Flutter sync unnecessary; endpoint was never registered

## Delivery
State: Needs merge
Branch/PR: cursor/backend-test-027-question-endpoints-fa87
Commit SHA: self
Completion %: 100

# BACKEND-TEST-027 Refresh Evidence — 2026-09-23

Evidence format: v2
Prompt ID: BACKEND-TEST-027
Run mode: current-main refresh
Base main: `678ac0232b4d0b0d39f301ebf0a9e2649d1e2058`
Source historical PR: #22 / `cursor/backend-test-027-question-endpoints-fa87`
Refresh branch: `agent/BACKEND-TEST-027-refresh-20260923`

## Current-main re-audit

- `QuestionEndpoints.cs` still existed on current main but was not mapped by `Program.cs`.
- The file shared the `/api/questions` family with authoring and retained an unbounded `limit` argument if someone accidentally wired it later.
- Current endpoint inventory still described the family as dead/unwired and left the decision open.

## Change

- Removed `src/MathLearning.Api/Endpoints/QuestionEndpoints.cs`.
- Carried forward the previously focused absence regression suite:
  - dead type absent from API assembly;
  - `Program.cs` does not register `MapQuestionEndpoints`;
  - no legacy `GetQuestions` / `GetQuestion` endpoint names;
  - learner GET cannot expose the removed unbounded list.
- Updated only current canonical inventory/queue rows.

## Validation state

Historical focused validation on the same behavioral change: 10/10 passed (`QuestionEndpointsAbsence|QuestionAuthoringAuthorization`).
Fresh current-main GitHub CI must still execute before merge; do not treat historical proof alone as current-main executable evidence.

## Residual risk

Low and bounded: endpoint was never registered. Cross-repo Flutter sync is not required because no shipped mobile route is being removed.

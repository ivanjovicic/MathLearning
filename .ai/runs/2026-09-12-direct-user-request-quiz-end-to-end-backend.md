Prompt contract: v2
Prompt ID: direct-user-request-quiz-end-to-end-backend-2026-09-12
Queue: direct-user-request
Agent/tool: Codex desktop + local shell
Tool lane: implementation / focused validation
Model provider: OpenAI
Model name/id: GPT-5
Model mode/settings: default
Client/IDE: Codex desktop
Run mode: investigation
Token budget: high
Run timebox: 30 minutes
Elapsed minutes: 30
Timebox result: within
Deadline action: completed
Actual context: high
Efficiency metrics: workflow_reads=6; source_reads=18; changed=12; searches=11; full_diffs=2; validation_runs=4; failed_retries=0; context_score=8
Prompt interpretation: matched
Interpretation note: Backend changes are limited to content playability gating and explicit no-content HTTP semantics; no answer-truth or economy contract was changed.
Documentation impact: updated docs/API_ENDPOINT_INVENTORY.md, docs/backend_contract_gap_report.md, docs/mobile_api_contract.md
Execution packet adherence: matched
Reads outside packet: none
Hypothesis changes: 1
Scope validation: passed - `git diff --check` and focused .NET integration tests
Test-first result: exception
Started from queue status: direct-user-request
Local collision check: isolated clean worktree from origin/main; original user workspace preserved
Relevant prior mistakes read: MISTAKE-VALIDATION-001
How this run avoids prior mistakes: authenticated production evidence is not fabricated; the code-level gate is covered by integration tests and the gap report remains pending for live content.
Delivery target: main
Delivery mode: pull-request
Working branch: codex/quiz-content-playability-20260912
Delivery status: pending-merge
Delivery PR: pending creation
Main commit SHA: not on main; branch commit ae7241a
Main verification: not run - branch is not merged and authenticated content audit is unavailable

## Deadline checkpoints

- Confirmed `/api/progress/topics` uses top-level topic ids and `/api/quiz/start` uses subtopic ids.
- Confirmed current adaptive practice uses `/api/practice/session/*` envelope and DTO option objects.
- Added a shared playable-content predicate before quiz/adaptive question selection.

## Files inspected

- Quiz/practice endpoints, `EfQuestionSelector`, `PracticeSessionService`, API result HTTP mapper.
- Quiz contract integration tests, domain question publish/delete behavior, endpoint inventory and mobile contract docs.

## Files changed

- Added `PlayableQuestionQuery` for published/non-deleted/structurally playable selection.
- Applied the gate to classic start, legacy questions, next-question, and adaptive selector.
- Prevented practice sessions from being persisted without a first question.
- Mapped `NO_PLAYABLE_QUESTIONS` to HTTP 404 and added endpoint/content contract tests.
- Updated endpoint and handoff docs without changing legacy pre-answer answer-key behavior.

## Commands run

- `validation | dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizStartContractIntegrationTests|FullyQualifiedName~PracticeSessionServiceIntegrationTests"` -> pass (16)
- `validation | git diff --check` -> pass

## Test-first proof

- Reason: no deterministic pre-edit red test was captured for the production content database; the live content audit requires unavailable authentication/export data. The implementation has focused integration counterexamples.
- Alternative proof: endpoint tests cover empty content, draft/deleted exclusion, topic-key selection, pre-answer shape, and adaptive no-content HTTP 404; service tests cover start/answer/complete persistence.
- Follow-up: backend/content owner must execute the authenticated three-topic audit.

## What was done

- All current user-facing quiz selectors share the same publish, delete, prompt, options, and exactly-one-correct-option gate.
- Classic and legacy empty content return `NO_PLAYABLE_QUESTIONS` instead of creating an unusable session.
- Practice start returns an `ApiResult` 404 for no playable content; the mobile adaptive provider can render a specific recovery action.
- The gap report explicitly says code verification is complete while live content verification remains pending.

## What was missed

- No authenticated `GET /api/progress/topics` or three real question requests could be executed.
- No DB export/content audit was available to prove every unlocked topic has a playable question.

## Counterexample review

- Draft and soft-deleted questions are excluded from classic selection.
- Questions with blank text, fewer than two non-empty options, or not exactly one correct option are excluded.
- Empty practice start is 404 and does not persist an active session.

## Changed-file safety review

- No migration, production data, economy, rewards, Daily Run, or answer evaluation changes.
- No token, user data, or production credentials were accessed or logged.

## Proof executed

- 16 focused integration/service tests passed.

## Validation run

- `dotnet test` focused contract/service filter: pass.
- Build completed as part of the test command.

## Validation not run

- Full .NET suite, live authenticated content audit, and production DB audit: not run; not available or not required for this scoped patch.

## Waste categories

- none

## Prompt defects observed

- The prior handoff treated the code-level route contract as sufficient for live playability; docs now separate code verification from real content verification.

## Documentation checked

- `docs/API_ENDPOINT_INVENTORY.md`, `docs/backend_contract_gap_report.md`, and `docs/mobile_api_contract.md` updated to reflect 404/error-code and playable-content semantics.

## Mistakes observed

- MISTAKE-VALIDATION-001 avoided by keeping the live audit explicitly pending instead of inferring it from tests.

## Learning classification

- Same-owner backend contract/content gate repair; no new queue prompt required.

## Where time/context was wasted

- none

## What the next agent should avoid

- Do not mark the backend handoff verified from unauthenticated 401 results or fixtures alone.
- Do not expose `correctAnswerId` in pre-answer responses to solve mobile parsing.

## Docs/rules updated to prevent repeat

- Endpoint inventory and gap report now name the exact remaining authenticated evidence.

## Queue updated

- direct-user-request; no formal queue claim was created.

## Follow-up coverage

- none

## Closure verdict

- Done allowed: no - PR delivery and live content evidence are pending.
- Backend code is ready for review and mobile integration.

## Completion %

75% - code and focused proofs complete; live data verification/main delivery pending.

## Residual risk

- Existing production content may not satisfy the new playable gate, which will correctly surface a specific no-content state until content is published/fixed.

## Commit SHA

- ae7241a

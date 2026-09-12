Evidence format: v2
Prompt ID: direct-user-request-quiz-end-to-end-backend-2026-09-12
Queue: direct-user-request
Agent/tool: Codex desktop + local shell
Model provider: OpenAI
Model name/id: GPT-5
Client/IDE: Codex desktop
Run mode: investigation
Token budget: high
Started at UTC: 2026-09-12T10:50:00Z
Completed at UTC: 2026-09-12T11:37:43Z
Elapsed time: 42m
Relevant prior mistakes read: BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: Live production evidence is not inferred from fixtures or anonymous 401 responses; focused executable tests and an explicit pending audit are recorded.
Owner/hypothesis: Backend quiz/practice content selection; one shared playable-question predicate plus explicit no-content responses prevents unusable sessions without inventing a mobile/API mapping.
Files inspected: 19
Files changed: 13
Searches: 11
Validation runs: 5
Failed retries: 0
Mistakes observed: BACKEND-MISTAKE-VALIDATION-001 (prevention=keep live content evidence pending and require executable focused validation before Done)
Waste: none
Missed: Authenticated three-topic content audit remains unavailable without a test account or anonymized export.
Follow-up: Backend/content owner must run the authenticated audit and confirm every unlocked topic has published playable content.
Residual risk: Existing production content may still fail the new playable gate until content is published or repaired.
Documentation impact: updated docs/API_ENDPOINT_INVENTORY.md, docs/backend_contract_gap_report.md, docs/mobile_api_contract.md
Cross-repo impact: Mobile contract synchronized in PR #685; classic topic-key and adaptive practice semantics are aligned without exposing pre-answer answer truth.
State: Needs validation
Branch/PR: codex/quiz-content-playability-20260912 -> PR #27
Commit SHA: ae7241a
Completion %: 75
Validation run: dotnet focused contract/service tests pass (16), Inline-Latex contract test passes (1), and local evidence validator passes; prior CI evidence-schema issue corrected.

## Outcome

Make classic quiz and learning-map adaptive practice return playable content or a precise recoverable no-content result.

## Contract decisions

- `/api/progress/topics` exposes top-level topic ids; classic legacy question loading accepts `topic_<Topic.Id>` and explicit `subtopicId`.
- Canonical `/api/quiz/start` remains subtopic-based; adaptive practice remains `/api/practice/session/*`.
- Pre-answer responses continue to omit `correctAnswerId`; no answer-truth or economy behavior was changed.

## Implementation

- Added `PlayableQuestionQuery.WherePlayable()` for published, non-deleted questions with meaningful text, at least two non-empty options, and exactly one correct option.
- Applied the gate to classic start, legacy questions, next-question, and adaptive selection.
- Prevented persistence of practice sessions without a first question.
- Mapped `NO_PLAYABLE_QUESTIONS` to HTTP 404 and updated focused contract tests.
- Updated the inline-LaTeX endpoint fixture to contain two published options, matching the playable-content contract.

## Proof

- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --no-restore --filter "FullyQualifiedName~QuizStartContractIntegrationTests|FullyQualifiedName~PracticeSessionServiceIntegrationTests"` -> pass (16).
- `git diff --check` -> pass.
- `python scripts/validate_agent_evidence.py --changed-from 8faa5b9f75d9d3e607f74f3907df2b709f0606e8 --verify-git` -> pass (0 failures; compact-log warning resolved by this rewrite).
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --no-restore --filter "FullyQualifiedName~InlineLatexEndpointContractTests"` -> pass (1).

## Not run / handoff

- Full .NET suite, authenticated `/api/progress/topics`, three real question requests, and production DB audit were not run because no token or anonymized export is available.
- Backend/content owner: provide the authenticated evidence and confirm each unlocked topic has at least one playable published question.
- No migration, production data, economy, rewards, Daily Run, token, user data, or credentials were touched.

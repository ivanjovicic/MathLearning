# direct-user-request-quiz-question-load-2026-09-12 Evidence

Evidence format: v2
Prompt ID: direct-user-request-quiz-question-load-2026-09-12
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: investigation
Token budget: low
Started at UTC: 2026-09-12T07:55:26Z
Completed at UTC: 2026-09-12T07:55:36Z
Elapsed time: 0m 10s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-XREPO-001; apply BACKEND-MISTAKE-AUDIT-001
Owner/hypothesis: open
Files inspected: 14
Files changed: 0
Searches: 2
Validation runs: 2
Failed retries: 0

## Outcome
- Follow-up cross-repository audit found that the mobile protection logic is internally consistent, but the current backend/mobile contract is not shippable for classic online completion: the backend pre-answer payload omits `correctAnswerId`, while mobile rejects such content as non-playable; the backend also returns a server `quizId` that mobile discards and replaces with a local synthetic ID.
- Live authenticated content evidence remains blocked by missing token; published/playable filtering is not guaranteed in legacy/practice selectors.

## Changed paths
- none

## Validation
Validation run: dotnet test ... --filter FullyQualifiedName~QuizStartContractIntegrationTests: PASS 8/8; QuizEndpointDataTests: PASS 8/8; PracticeSessionServiceIntegrationTests: PASS 5/5; mobile focused loading/cache/Learning Map tests: PASS 23/23; live GET /api/progress/topics without auth: HTTP 401; no token available
Validation failure: mobile `flutter analyze`: failed on missing generated Drift files plus unrelated current-main test/API mismatches; full focused mobile command including `quiz_session_route_semantics_test.dart` failed to compile because `TestCoinProvider` lacks two current `CoinProvider` getters.
Validation not run: none

## Exceptions and learning
Mistakes observed: none
Waste: none
Missed: none
Follow-up: backend/content owner must confirm the question envelope, correct-answer policy, server-issued quiz identity, published filtering, and unlocked-topic content; mobile owner must preserve/use the returned `quizId` and add an end-to-end answer test.
Residual risk: classic online and cache-fallback answer submission can fail with `Quiz session not found`; Learning Map remains intentionally gated until its content identity contract is confirmed.
Documentation impact: none
Cross-repo impact: no

## Delivery
State: Needs handoff
Branch/PR: Needs backend/content owner handoff; no branch/PR created
Commit SHA: self
Completion %: 70

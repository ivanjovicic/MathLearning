# Backend Quiz Review — 2026-06-27

Status: planning / prompt input, not runtime evidence  
Repo: `ivanjovicic/MathLearning`

This review records new quiz/offline-submit risks found after inspecting `QuizEndpoints.cs` and related mobile docs.

---

## 1. Inputs inspected

- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- `docs/BACKEND_REVIEW_2026_06_27.md`
- `docs/API_ENDPOINT_INVENTORY.md`
- mobile repo `docs/NEW_RISK_ANALYSIS_2026_06_27.md`
- mobile repo `lib/state/quiz_provider.dart`
- mobile repo `lib/services/offline_manager.dart`

---

## 2. New backend risks

| Priority | Risk | Why it matters | Prompt |
|---:|---|---|---|
| P0 | Legacy `/api/quiz/batch-submit` sends a new random session id into batch processing | Replay/idempotency behavior is weaker than the modern typed offline-submit path | `BE-CONTRACT-008` |
| P1 | Quiz count inputs do not have a clear upper-bound policy at API boundary | Large or bad request counts can hurt latency and create inconsistent UX | `BE-CONTRACT-009` |
| P1 | Mobile still uses mixed modern and legacy quiz submit paths | Backend and mobile contract cleanup should be coordinated | `BE-CONTRACT-008` |
| P2 | Random question selection uses a wrapped contiguous block from ordered IDs | It is efficient, but may reduce content variety versus expected random sampling | `BE-CONTRACT-009` |

---

## 3. Prompt: BE-CONTRACT-008 — Quiz/offline submit idempotency and legacy alias hardening

```text
Use only this repository:
ivanjovicic/MathLearning

Task:
Harden quiz answer/offline submit replay behavior and align legacy aliases with the canonical mobile contract.

Before editing, inspect:
- AGENTS.md
- docs/BACKEND_REVIEW_2026_06_27.md
- docs/BACKEND_QUIZ_REVIEW_2026_06_27.md
- docs/API_ENDPOINT_INVENTORY.md
- docs/backend_contract_gap_report.md
- src/MathLearning.Api/Endpoints/QuizEndpoints.cs
- src/MathLearning.Api/Endpoints/QuizEndpointHelpers.cs
- idempotency ledger services/tests
- tests covering quiz answer, offline submit, batch submit, and XP awards

Inspect mobile repo for context only:
- docs/NEW_RISK_ANALYSIS_2026_06_27.md
- docs/mobile_api_contract.md
- docs/mobile_backend_contract_status.md
- lib/services/offline_manager.dart
- lib/services/quiz_api_service.dart

Owned paths:
- backend quiz endpoint files
- backend quiz/idempotency/contract tests
- backend endpoint inventory/gap docs if behavior changes

Avoid paths:
- Flutter repo
- broad quiz redesign
- unrelated economy/cosmetics endpoints

Required work:
1. Define the canonical route for mobile offline batch submit.
2. Parse stable `sessionId`, `quizId`, `batchId`, or `operationId` from legacy payload when present.
3. Decide whether `/batch-submit` should remain, become a thin adapter to `/offline-submit`, or be deprecated with tests.
4. Add tests for replaying the same batch twice, app restart retry, duplicated answers, malformed session id, mixed valid/invalid answers, and XP not double-awarded.
5. Ensure response shape stays compatible with mobile until mobile contract cleanup lands.
6. Update backend docs honestly; leave mobile status sync for a cross-repo evidence prompt.

Validation:
- git diff --check
- targeted dotnet tests for quiz/offline submit/idempotency

Final response:
Changed:
Replay/idempotency decision:
Validation:
Residual risk:
Commit:
Next recommended prompt:
```

---

## 4. Prompt: BE-CONTRACT-009 — Quiz count bounds and question selection guardrails

```text
Use only this repository:
ivanjovicic/MathLearning

Task:
Add or document request bounds for quiz question count and improve question-selection guardrails.

Before editing, inspect:
- docs/BACKEND_REVIEW_2026_06_27.md
- docs/BACKEND_QUIZ_REVIEW_2026_06_27.md
- docs/API_ENDPOINT_INVENTORY.md
- src/MathLearning.Api/Endpoints/QuizEndpoints.cs
- StartQuizRequest / quiz DTO definitions
- tests covering /api/quiz/start and legacy /api/quiz/questions GET/POST

Owned paths:
- backend quiz endpoint files
- quiz request validators or helper methods
- backend endpoint tests
- backend endpoint inventory docs if behavior changes

Avoid paths:
- Flutter repo
- unrelated progress/economy logic
- broad adaptive algorithm changes unless required by tests

Required work:
1. Define min/max quiz count policy, for example 1..25 or 1..50.
2. Apply the same policy to `/api/quiz/start`, `/api/quiz/questions` GET, and `/api/quiz/questions` POST.
3. Decide whether out-of-range values return 400 or are normalized; keep behavior consistent.
4. Add endpoint tests for count 0, negative, normal, over max, and no questions available.
5. Review `SelectRandomQuestionIdsAsync` content variety and document whether contiguous wrapped selection is intentional or needs later improvement.
6. Update endpoint inventory/gap docs.

Validation:
- git diff --check
- targeted dotnet tests for quiz endpoints

Final response:
Changed:
Bounds decision:
Validation:
Residual risk:
Commit:
Next recommended prompt:
```

---

## 5. Not implemented here

This file only adds review findings and implementation prompts. It does not change endpoint behavior, validators, tests, or mobile contract status.

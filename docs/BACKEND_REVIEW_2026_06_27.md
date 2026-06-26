# Backend Review — 2026-06-27

Status: planning / prompt input, not runtime evidence  
Repo: `ivanjovicic/MathLearning`

This review records backend risks found while inspecting current endpoint inventory and targeted code paths. It is intended to drive concrete hardening prompts before mobile UI surfaces consume the backend more broadly.

---

## 1. Inputs inspected

- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/DOCS_INDEX.md`
- `src/MathLearning.Api/Endpoints/ExplanationEndpoints.cs`
- `src/MathLearning.Api/Services/StepExplanationService.cs`
- `src/MathLearning.Api/Services/AiTutorEnhancer.cs`
- `src/MathLearning.Api/Services/ExplanationCacheService.cs`
- `src/MathLearning.Application/DTOs/Explanations/ExplanationDtos.cs`
- `src/MathLearning.Application/Validators/GenerateExplanationRequestValidator.cs`
- related mobile cross-repo docs in `ivanjovicic/Mathlearning-Mobile-App`

---

## 2. Highest-priority backend risks

| Priority | Risk | Why it matters | Prompt |
|---:|---|---|---|
| P0 | Explanation routes are already exposed under `/api/explanations` | Mobile may connect a child-facing explanation UI before safety/validation/rate-limit/cache policy is ready | `BE-CONTRACT-007` |
| P0 | Free-form explanation generation exists | Free-form problem text can include unsupported or personal content | `BE-CONTRACT-007` |
| P0 | Request validation is length/presence-focused only | Grade range, supported language, and difficulty constraints need explicit tests | `BE-CONTRACT-007` |
| P0 | `EnableAiTutorEnhancement` defaults to true in request DTO | Server should control AI/explanation enhancement policy for child-facing use | `BE-CONTRACT-007` |
| P1 | Read/runtime parity matrix is still coarse for non-P0 endpoint families | Mobile reads can drift even when mutation/idempotency endpoints are strong | `BE-CONTRACT-006` |
| P1 | Free-form explanation cache persists generated payloads | Cache/log/privacy policy must be explicit before child-facing release | `BE-CONTRACT-007` |

---

## 3. Prompt: BE-CONTRACT-007 — Explanation endpoint safety, validation, and cache hardening

```text
Use only this repository:
ivanjovicic/MathLearning

Task:
Harden explanation endpoints before any mobile child-facing UI calls them.

Before editing, inspect:
- AGENTS.md
- docs/API_ENDPOINT_INVENTORY.md
- docs/backend_contract_gap_report.md
- docs/BACKEND_REVIEW_2026_06_27.md
- src/MathLearning.Api/Endpoints/ExplanationEndpoints.cs
- src/MathLearning.Api/Services/StepExplanationService.cs
- src/MathLearning.Api/Services/AiTutorEnhancer.cs
- src/MathLearning.Api/Services/ExplanationCacheService.cs
- src/MathLearning.Application/DTOs/Explanations/ExplanationDtos.cs
- src/MathLearning.Application/Validators/GenerateExplanationRequestValidator.cs
- tests/MathLearning.Tests/Services/StepExplanationServiceIntegrationTests.cs
- existing endpoint/contract tests for auth/rate-limit patterns

Owned paths:
- explanation endpoint files
- explanation DTO validators
- explanation service/cache tests
- backend docs/API inventory or gap report if behavior changes

Avoid paths:
- Flutter repo
- open-ended AI chat implementation
- new external LLM integration
- logging raw child/free-form problem payloads
- changing unrelated quiz/progress endpoints

Required work:
1. Add validation for grade range, supported language values, difficulty values, and required expected answer policy where needed.
2. Decide whether `EnableAiTutorEnhancement` should default false or be server-policy-controlled for child-facing routes.
3. Add safe response/fallback behavior when generation cannot be trusted.
4. Add rate-limit/quota policy or document exact existing rate-limit coverage for explanation routes.
5. Prevent raw free-form child/problem payloads from being logged.
6. Review cache policy for free-form requests; consider no persistent DB cache for free-form child input unless explicitly safe.
7. Add tests for invalid grade, unsupported language, invalid difficulty, too-long input, missing expected answer, cache served flag, and no-AI fallback.
8. Update backend docs honestly; leave mobile contract/status sync to cross-repo evidence docs after commit.

Validation:
- git diff --check
- targeted dotnet tests for explanation validators/services/endpoints
- if docs-only, verify referenced files exist

Final response:
Changed:
Validation:
Safety decisions:
Cache/rate-limit decisions:
Residual risk:
Commit:
Next recommended prompt:
```

---

## 4. Prompt: BE-CONTRACT-006 — Backend read/runtime parity smoke matrix

```text
Use only this repository:
ivanjovicic/MathLearning

Task:
Create and execute a backend read/runtime parity smoke matrix for the mobile app's non-settlement endpoints.

Do not edit the Flutter repo in this prompt.
Do not mark a route verified without endpoint evidence, test evidence, or explicit smoke evidence.

Before editing, inspect:
- README.md
- docs/mobile_contract_idempotency_handoff.md
- docs/backend_contract_gap_report.md
- docs/API_ENDPOINT_INVENTORY.md
- docs/BACKEND_REVIEW_2026_06_27.md
- Program.cs
- endpoint files for auth, users/profile, progress, adaptive/practice, leaderboard, cosmetics reads, hints, sync
- existing integration/contract tests

Owned paths:
- backend docs/read_runtime_parity_smoke_matrix.md or similar
- backend integration/contract tests if gaps are small and safe to cover
- backend endpoint inventory/status docs

Avoid paths:
- Flutter repo
- broad endpoint redesign
- unrelated migrations
- changing P0 settlement behavior unless a regression is found

Required work:
1. List read/runtime endpoint families used by mobile: auth, current profile, public profile, progress overview/topics/week activity, adaptive path/recommendations/reviews, practice session start/answer/complete shape, leaderboard users/schools/history, cosmetics catalog/inventory/avatar reads, hints, sync reads if present.
2. For each route, record method/path, auth behavior, owner file, mobile caller if known, expected response shape, empty-state behavior, and test/smoke evidence.
3. Separate verified, likely exists but unverified, compatibility-only, unsupported, and not-run rows.
4. Add targeted backend tests only for small high-value gaps if safe.
5. Update backend gap report or endpoint inventory with honest status.
6. Leave mobile docs/status update to a later cross-repo evidence sync prompt.

Validation:
- git diff --check
- targeted dotnet tests if code/tests changed
- if docs-only, verify referenced endpoint/test files exist

Final response:
Changed:
Parity matrix:
Validation:
Unverified routes:
Residual risk:
Commit:
Next recommended prompt:
```

---

## 5. Not implemented here

This file only adds review findings and implementation prompts. It does not change runtime endpoint behavior, validators, cache policy, tests, or mobile contract status.

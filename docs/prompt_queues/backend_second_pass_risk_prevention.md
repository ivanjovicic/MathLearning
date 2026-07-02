# Backend Second-Pass Risk Prevention Prompt Queue

Last aligned: 2026-07-01  
Target repo: `ivanjovicic/MathLearning`  
Lane: second-pass backend/API risk prevention  
Owns: proxy/rate-limit trust, refresh-token concurrency, registration atomicity, authoring authorization, adaptive input bounds, recurring job idempotency, admin seeding safety, question version races  
Avoids: duplicate `BACKEND-CRIT-001..008`, mobile repo runtime changes, broad auth rewrites, schema changes without migration/test evidence

Read first:

- `../AGENTS.md`
- `../DOCS_INDEX.md`
- `../ARCHITECTURE_OVERVIEW.md`
- `../API_ENDPOINT_INVENTORY.md`
- `../BACKEND_CHANGE_CHECKLIST.md`
- `../COMMON_AGENT_PITFALLS.md`
- `../BACKEND_CRITICAL_RISK_PREVENTION_RULES.md`
- `../BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md`
- `../BACKEND_SECOND_PASS_RISK_PREVENTION_RULES.md`
- `../BACKEND_SECOND_PASS_APP_FLOW_AUDIT_2026_07_01.md`
- `../AGENT_RUN_LOG_ENFORCEMENT.md`
- `../ai/learning/MISTAKE_LEDGER.md`

Hard rules:

```text
Do not duplicate BACKEND-CRIT-001..008.
Do not trust forwarded headers from arbitrary clients.
Do not let spoofed X-Forwarded-For bypass rate limiting.
Do not let one refresh token mint multiple active descendants under concurrency.
Do not leave orphan Identity/Profile/RefreshToken state after registration failure.
Do not allow normal authenticated learners to publish or mutate question authoring content.
Do not accept unbounded adaptive confidence/responseTime/timestamp/answer inputs.
Do not schedule recurring job logic without idempotency/non-overlap evidence.
Do not reset privileged admin credentials in production without explicit emergency-safe guardrails.
Do not generate draft/version numbers without race-safety tests.
Every Done row must name the backend risk prevented and exact integration/contract/concurrency test added.
```

---

## Queue status model

| Status | Meaning |
|---|---|
| **Audit-created** | Finding in `BACKEND_SECOND_PASS_APP_FLOW_AUDIT_2026_07_01.md`; no runtime work started. |
| **Prompt-ready** | Prompt below is ready when prerequisites met. |
| **Runtime-fixed** | `src/**` or `tests/**` changed under this prompt ID. |
| **Validated** | Tests passed and recorded in `.ai/runs` evidence. |

### Done requirements

```text
Done requires: runtime/test commit + `.ai/runs/<date>-<prompt-id>-evidence.md` + validation result.
Docs/spec-only work: max Done 85%; not Runtime-fixed.
```

### Recommended execution order

**Security / auth / data-loss** first:

```text
BACKEND2-CRIT-004 → BACKEND2-CRIT-001 → BACKEND2-CRIT-002 → BACKEND2-CRIT-003 → BACKEND2-CRIT-007
```

**Privacy / bounds** (where applicable):

```text
BACKEND2-CRIT-005
```

**Performance / jobs / docs-first specs**:

```text
BACKEND2-CRIT-006 → BACKEND2-CRIT-008
```

Run `BACKEND2-CRIT-008` after authoring policy (`BACKEND2-CRIT-004`) when publish races depend on auth policy.

---

## Active prompts

Rows include both **Done** and **Prompt-ready** entries.

| ID | Status | Can run in parallel with | Purpose |
|---|---|---|---|
| BACKEND2-CRIT-001 | Done (`aa83a3a`, 2026-06-24, validated) | — | Harden forwarded header trust and rate-limit identity. Run log: `.ai/runs/2026-06-24-BACKEND2-CRIT-001-evidence.md`. Tests: `RateLimitClientIdentityTests`, `InMemorySlidingWindowRateLimitMiddlewareTests`, `ForwardedHeadersProxyTrustIntegrationTests`. Risk: proxy-trust-boundary / rate-limit spoofing. |
| BACKEND2-CRIT-002 | Done (`79ea851`, 2026-07-01, validated) | — | Make refresh-token rotation single-use under concurrency. Run log: `.ai/runs/2026-07-01-BACKEND2-CRIT-002-evidence.md`. Tests: `AuthRefreshConcurrencyTests`, `AuthRefreshEndpointRegressionTests`. Risk: refresh-token-rotation-race. |
| BACKEND2-CRIT-003 | Done (`b073350`, 2026-07-01, validated) | — | Make mobile registration atomic or compensating. Run log: `.ai/runs/2026-07-01-BACKEND2-CRIT-003-evidence.md`. Tests: `AuthMobileRegistrationAtomicityTests`. Risk: auth-registration-atomicity. |
| BACKEND2-CRIT-004 | Done (`aa83a3a`, 2026-06-24, validated) | — | Add explicit admin/content-author policy to question authoring mutations. Run log: `.ai/runs/2026-06-24-BACKEND2-CRIT-004-evidence.md`. Tests: `QuestionAuthoringAuthorizationTests`. Risk: authoring-authorization. |
| BACKEND2-CRIT-005 | Done (`aa83a3a`, 2026-06-24, validated) | — | Bound adaptive answer inputs. Run log: `.ai/runs/2026-06-24-BACKEND2-CRIT-005-evidence.md`. Tests: `AdaptiveAnswerInputBoundsTests`, `AdaptiveAnswerBoundsEndpointTests`. Risk: adaptive-input-bounds. |
| BACKEND2-CRIT-006 | Done (`aa83a3a`, 2026-06-24, validated) | — | Define/test recurring job idempotency and non-overlap. Run log: `.ai/runs/2026-06-24-BACKEND2-CRIT-006-evidence.md`. Tests: `PracticeHangfireJobsTests`, `SchoolLeaderboardSnapshotIdempotencyTests`, `AnswerPatternAntiCheatServiceTests`. Risk: recurring-job-overlap. |
| BACKEND2-CRIT-007 | Done (`aa83a3a`, 2026-06-24, validated) | — | Harden production admin seeding/reset behavior. Run log: `.ai/runs/2026-06-24-BACKEND2-CRIT-007-evidence.md`. Tests: `SeedAdminStartupPolicyTests`. Risk: admin-seed-hardening. |
| BACKEND2-CRIT-008 | Done (`aa83a3a`, 2026-06-24, validated) | — | Make question draft/version numbering race-safe. Run log: `.ai/runs/2026-06-24-BACKEND2-CRIT-008-evidence.md`. Tests: `QuestionAuthoringVersionConcurrencyTests`, `QuestionAuthoringPipelineTests`. Migration: `20260702152409_AddQuestionVersionSourceDraftUniqueIndex`. Risk: authoring-version-race. |

---

## BACKEND2-CRIT-001 — Proxy trust and rate-limit spoofing tests

Run mode: implementation/test or config/spec-first if hosting boundary is unclear  
Token budget: medium

Task: ensure forwarded headers cannot be spoofed to bypass IP-based rate limiting or alter request identity unexpectedly.

Backend second-pass risk rules:

- Risk class: proxy-trust-boundary.
- Do not trust X-Forwarded-* headers from arbitrary clients.
- Rate limiting must key off a trustworthy client identity.

Before editing, inspect:

- `docs/BACKEND_SECOND_PASS_APP_FLOW_AUDIT_2026_07_01.md`
- `docs/BACKEND_SECOND_PASS_RISK_PREVENTION_RULES.md`
- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Middleware/InMemorySlidingWindowRateLimitMiddleware.cs`
- hosting/Fly docs or deployment config if present in repo

Owned paths:

- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Middleware/InMemorySlidingWindowRateLimitMiddleware.cs`
- targeted middleware/startup tests
- docs inventory/config note if behavior changes
- this queue status row only

Required work:

1. Add tests showing spoofed `X-Forwarded-For` cannot create unlimited rate-limit buckets.
2. Configure known proxy/network trust or document platform stripping evidence.
3. Consider user-id-based rate limiting for authenticated endpoints if IP identity is not trustworthy.
4. Preserve health/metrics exemption.
5. Update docs if deployment config is required.

Validation:

```bash
dotnet test --filter "RateLimit|ForwardedHeaders|Proxy"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND2-CRIT-001-evidence.md` with middleware/proxy tests or documented hosting config evidence.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001

---

## BACKEND2-CRIT-002 — Refresh-token rotation race hardening

Run mode: implementation/test  
Token budget: medium/high

Task: make refresh token rotation single-use under concurrent requests and define reuse behavior.

Backend second-pass risk rules:

- Risk class: refresh-token-rotation-race.
- Refresh-token rotation must be single-use under concurrent requests.
- A token already revoked/rotated must not mint another token.

Before editing, inspect:

- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `src/MathLearning.Infrastructure/Services/RefreshTokenService.cs`
- `src/MathLearning.Domain/Entities/RefreshToken.cs`
- refresh-token migrations/indexes
- auth integration tests

Owned paths:

- auth endpoint/service files
- refresh-token entity/migration only if needed
- targeted auth concurrency tests
- this queue status row only

Required work:

1. Add concurrency test: two refresh requests with same token produce exactly one success.
2. Second refresh/reuse must return invalid/expired and create no active descendant token.
3. Ensure logout/revoke-all still works.
4. Add transaction/row lock/concurrency token/conditional update strategy.
5. Do not log token values.

Validation:

```bash
dotnet test --filter "RefreshToken|Auth|Concurrency"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND2-CRIT-002-evidence.md` with concurrency test proof.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001

---

## BACKEND2-CRIT-003 — Mobile registration atomicity

Run mode: implementation/test  
Token budget: medium/high

Task: prevent partial Identity/Profile/RefreshToken state when mobile registration fails mid-flow.

Backend second-pass risk rules:

- Risk class: auth-registration-atomicity.
- Registration/login flows must not leave orphan Identity users, profiles, or refresh tokens after partial failures.
- Welcome bonus must not be double-granted under retry.

Before editing, inspect:

- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- Identity/ApiDbContext transaction behavior
- `UserProfile` creation/migrations
- registration tests

Owned paths:

- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- targeted registration integration tests
- docs note if behavior changes
- this queue status row only

Required work:

1. Add test: profile save failure does not leave orphan Identity user.
2. Add test: refresh-token save failure does not leave account appearing fully registered without token.
3. Add test: retry after partial failure does not double-grant welcome coins.
4. Implement transaction or compensating cleanup.
5. Keep client error safe/generic.

Validation:

```bash
dotnet test --filter "MobileRegister|Registration|Auth|Atomicity"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND2-CRIT-003-evidence.md` with atomicity/retry tests.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001

---

## BACKEND2-CRIT-004 — Question authoring authorization policy

Run mode: implementation/test  
Token budget: medium

Task: restrict question authoring mutation routes to admin/content-author policy and document exact policy in endpoint inventory.

Backend second-pass risk rules:

- Risk class: authoring-authorization.
- Content authoring mutation routes must require explicit admin/content-author policy, not generic authentication.

Before editing, inspect:

- `src/MathLearning.Api/Endpoints/QuestionAuthoringEndpoints.cs`
- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- `src/MathLearning.Infrastructure/Services/QuestionAuthoring/MathQuestionAuthoringService.cs`
- authoring endpoint tests
- admin policy docs/inventory

Owned paths:

- `src/MathLearning.Api/Endpoints/QuestionAuthoringEndpoints.cs`
- policy registration only if a new ContentAuthor policy is needed
- targeted authorization tests
- `docs/API_ENDPOINT_INVENTORY.md`
- this queue status row only

Required work:

1. Add tests proving normal authenticated learner cannot `save-draft`, `publish`, or `revalidate`.
2. Add tests proving admin/content-author can perform allowed actions.
3. Decide if `validate` and `preview` are content-author-only or regular-auth allowed.
4. Update endpoint inventory with exact policy.
5. Ensure unauthorized attempts create no draft/version/audit rows.

Validation:

```bash
dotnet test --filter "QuestionAuthoring|Authorization|Admin"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND2-CRIT-004-evidence.md` + `docs/API_ENDPOINT_INVENTORY.md` policy update.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001

---

## BACKEND2-CRIT-005 — Adaptive answer input bounds

Run mode: implementation/test  
Token budget: medium

Task: clamp or reject unsafe adaptive answer inputs before they reach scoring/model/storage logic.

Backend second-pass risk rules:

- Risk class: adaptive-input-bounds.
- Adaptive answer inputs must clamp or reject invalid confidence, response time, timestamp, and answer lengths.

Before editing, inspect:

- `src/MathLearning.Api/Endpoints/AdaptiveEndpoints.cs`
- `src/MathLearning.Api/Services/AdaptiveApiFacade.cs`
- adaptive service/model tests
- mobile adaptive contract docs if present

Owned paths:

- `src/MathLearning.Api/Endpoints/AdaptiveEndpoints.cs`
- targeted adaptive endpoint tests
- docs contract note if validation semantics change
- this queue status row only

Required work:

1. Add tests for confidence below 0, above 1, NaN/string invalid, and huge values.
2. Add tests for unreasonable response time seconds/ms.
3. Add tests for future/past `answeredAt` if adaptive uses timestamps.
4. Add max answer length validation.
5. Keep response shape as safe `VALIDATION_ERROR`.

Validation:

```bash
dotnet test --filter "Adaptive|Validation|AnswerBounds"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND2-CRIT-005-evidence.md` with bounds validation tests.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001 (if adaptive contract docs change)

---

## BACKEND2-CRIT-006 — Recurring job idempotency and non-overlap

Run mode: investigation/spec first, implementation if services expose clear gaps  
Token budget: medium/high

Task: document and test idempotency/non-overlap behavior for recurring Hangfire jobs.

Backend second-pass risk rules:

- Risk class: recurring-job-overlap.
- Recurring background jobs must be idempotent, non-overlapping where needed, and safe when a previous run is still active.

Before editing, inspect:

- `src/MathLearning.Api/Program.cs`
- `IPracticeHangfireJobs` / implementation
- `ISchoolLeaderboardHangfireJobs` / implementation
- `IAntiCheatHangfireJobs` / implementation
- background job tests and migrations/indexes

Owned paths:

- `docs/BACKGROUND_JOB_IDEMPOTENCY_SPEC.md`
- targeted job service tests if implementation is clear
- job service files only if tests expose a clear bug
- this queue status row only

Required work:

1. Document each recurring job name, schedule, business key, and overlap policy.
2. Add tests for practice daily aggregation re-run same day.
3. Add tests for school leaderboard snapshot unique period/time bucket.
4. Add tests for anti-cheat sweep claiming rows once under concurrent workers.
5. Add migration/unique index follow-up prompts if needed.

Validation:

```bash
dotnet test --filter "Hangfire|BackgroundJob|SchoolLeaderboard|AntiCheat|Aggregation"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND2-CRIT-006-evidence.md`.
- `docs/BACKGROUND_JOB_IDEMPOTENCY_SPEC.md` alone = spec (≤85%); job code changes need tests.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001

---

## BACKEND2-CRIT-007 — Admin seeding production hardening

Run mode: implementation/test or docs/config-first  
Token budget: medium

Task: ensure production admin seeding and reset-on-start behavior cannot accidentally create/reset privileged credentials.

Backend second-pass risk rules:

- Risk class: admin-seed-hardening.
- Production/admin seeding must never silently reset privileged credentials or create/default privileged users without explicit safe configuration and audit logs.

Before editing, inspect:

- `src/MathLearning.Api/Program.cs` `SeedAdminUser`
- config files/deployment docs
- admin/auth tests

Owned paths:

- `src/MathLearning.Api/Program.cs`
- targeted startup/config tests if feasible
- docs/config note
- this queue status row only

Required work:

1. Add tests/review evidence: production cannot seed admin with default/missing password.
2. Guard production `ResetPasswordOnStart` behind explicit emergency flag or disallow by default.
3. Ensure logs never include password values.
4. Preserve Development convenience behavior.
5. Document production admin bootstrap process.

Validation:

```bash
dotnet test --filter "SeedAdmin|Startup|Admin"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND2-CRIT-007-evidence.md` with startup/config test or documented production guard proof.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001

---

## BACKEND2-CRIT-008 — Question draft/version race safety

Run mode: implementation/test or migration/spec-first if unique indexes are missing  
Token budget: medium/high

Task: make draft and published version numbering safe under concurrent save/publish operations.

Backend second-pass risk rules:

- Risk class: authoring-version-race.
- Draft/version numbers must be generated under transaction/unique constraints so concurrent saves/publishes cannot duplicate version numbers.

Before editing, inspect:

- `src/MathLearning.Infrastructure/Services/QuestionAuthoring/MathQuestionAuthoringService.cs`
- question draft/version entities and migrations
- authoring service tests

Owned paths:

- authoring service/tests
- migration only if unique constraints are missing and approved by evidence
- `docs/BACKGROUND_JOB_IDEMPOTENCY_SPEC.md` not relevant unless jobs change
- this queue status row only

Required work:

1. Add concurrency test: two `SaveDraftAsync` calls for same question produce unique sequential draft versions.
2. Add concurrency test: two `PublishAsync` calls for same draft do not create duplicate published versions.
3. Add/verify unique indexes for `(QuestionId, DraftVersion)` and `(QuestionId, VersionNumber)` if needed.
4. Ensure audit logs and current pointers match committed version.
5. Ensure preview cache does not point to rolled-back content.

Validation:

```bash
dotnet test --filter "QuestionAuthoring|DraftVersion|Publish|Concurrency"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND2-CRIT-008-evidence.md` with concurrency tests and migration evidence if indexes added.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001

---

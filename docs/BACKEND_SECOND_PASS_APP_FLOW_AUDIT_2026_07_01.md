# Backend Second-Pass App Flow Audit — 2026-07-01

> **Static audit only — not fix proof.**
> This document records second-pass inspection findings and creates **prompt-ready** follow-ups.
> It does **not** prove runtime fixes landed and did **not** execute `dotnet test`.
> See `docs/prompt_queues/backend_second_pass_risk_prevention.md` for implementation status.

Status: static backend repo/code audit (audit-created)
Repo: `ivanjovicic/MathLearning`  
Scope: second-pass backend/API risks not covered by `BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md`  
Validation: docs/code inspection only; no `dotnet test` executed in this audit

## Executive verdict

The first backend audit focused on safe errors, monitoring/log exposure, public identity minimization, avatar upload safety, settlement snapshot truth, idempotency requirements, offline timestamp bounds, and bounded reads.

This second pass found additional backend risk classes:

```text
unbounded forwarded-header trust can undermine IP-based rate limiting and request identity
refresh-token rotation can race under concurrent refresh requests
mobile registration can leave partial Identity/Profile/RefreshToken state after mid-flow failure
question authoring mutation routes require only authenticated user, not admin/content-author policy
adaptive answer payload accepts unbounded confidence/response-time/timestamp/answer values
frequent Hangfire jobs need explicit idempotency and non-overlap guarantees
production admin seeding/reset-on-start needs stronger guardrails and tests
question draft/version numbering can race under concurrent saves/publishes
```

These are potential risks, not confirmed production incidents.

---

## Finding BACKEND2-CRIT-001 — Forwarded header trust can undermine rate limiting

Severity: P1  
Type: proxy-trust-boundary / rate-limit bypass / request identity

Evidence from code:

- `Program.cs` configures forwarded headers for `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host`.
- It clears `KnownNetworks` and `KnownProxies`, which means trust boundary is broad unless the hosting platform strips spoofed headers first.
- The in-memory rate limiter keys buckets by `context.Connection.RemoteIpAddress`.
- `UseForwardedHeaders()` runs before the rate-limit middleware.

Potential failure:

```text
client spoofs X-Forwarded-For -> app treats spoofed IP as RemoteIpAddress -> rate limiter creates new bucket and can be bypassed.
```

Preventive action:

Add proxy trust/rate-limit tests and document hosting boundary. Use known proxy/network config or a safer rate-limit identity policy.

Follow-up prompt: `BACKEND2-CRIT-001`.

---

## Finding BACKEND2-CRIT-002 — Refresh-token rotation can race

Severity: P1  
Type: refresh-token-rotation-race / auth/session safety

Evidence from code:

- Refresh endpoint reads the token row by token value, validates it, calls `RefreshTokenService.RevokeToken(refreshToken)`, creates a new refresh token, adds it, and then saves changes.
- `RefreshTokenService.ValidateRefreshToken()` only checks null/revoked/expired state on the loaded entity.
- No obvious transaction, row lock, concurrency token, or atomic conditional update is visible in the refresh endpoint.

Potential failure:

```text
two refresh requests using the same old token arrive concurrently -> both validate before either SaveChanges -> both mint new active refresh tokens.
```

Preventive action:

Add concurrency tests and make refresh-token rotation single-use under race. Consider reuse detection/revoking descendants.

Follow-up prompt: `BACKEND2-CRIT-002`.

---

## Finding BACKEND2-CRIT-003 — Mobile registration is not obviously atomic

Severity: P1/P2  
Type: auth-registration-atomicity / partial account state

Evidence from code:

- Mobile register creates an Identity user.
- It then creates `UserProfile` and calls `db.SaveChangesAsync()`.
- It then creates a refresh token and calls `db.SaveChangesAsync()` again.
- The catch block returns generic failure, but no obvious transaction or compensating cleanup is visible around Identity/Profile/RefreshToken creation.

Potential failures:

```text
Identity user created -> profile save fails -> orphan Identity account.
profile saved -> refresh token save fails -> user exists but registration response fails/no token.
client retries -> welcome coins/profile state can drift unless guarded.
```

Preventive action:

Add registration atomicity tests and transaction/compensating cleanup strategy.

Follow-up prompt: `BACKEND2-CRIT-003`.

---

## Finding BACKEND2-CRIT-004 — Question authoring mutation routes are only authenticated

Severity: P1  
Type: authoring-authorization / content integrity

Evidence from code:

- `QuestionAuthoringEndpoints` maps `/api/questions` with `.RequireAuthorization()` only.
- Routes include `/save-draft`, `/publish`, and `/{id:int}/revalidate`, which mutate or publish content.
- The endpoint resolves actor user id from claims, but there is no obvious admin/content-author policy on the group or mutating routes.

Potential failure:

```text
any logged-in learner can save drafts, publish questions, or revalidate production content if endpoint is reachable.
```

Preventive action:

Add admin/content-author policy and tests proving normal users cannot mutate authoring content.

Follow-up prompt: `BACKEND2-CRIT-004`.

---

## Finding BACKEND2-CRIT-005 — Adaptive answer input bounds are under-specified

Severity: P1/P2  
Type: adaptive-input-bounds / scoring/model trust

Evidence from code:

- Adaptive answer parser accepts `confidence` as any parsed double and assigns it to request without clamping.
- It accepts `responseTimeSeconds` or `responseTimeMs` if non-negative but with no obvious upper bound.
- It accepts optional `answeredAt` using `DateTime.TryParse` without visible UTC normalization or bounds.
- It accepts answer string if non-empty, with no obvious maximum length.

Potential failure:

```text
client sends confidence=999, huge responseTimeMs, far-future answeredAt, or giant answer string -> downstream adaptive scoring/model/storage receives untrusted values.
```

Preventive action:

Add validation/bounds and tests for confidence, response time, timestamp, and answer length.

Follow-up prompt: `BACKEND2-CRIT-005`.

---

## Finding BACKEND2-CRIT-006 — Frequent recurring jobs need explicit non-overlap/idempotency guarantees

Severity: P1/P2  
Type: recurring-job-overlap / duplicated aggregation or reviews

Evidence from code:

- `Program.cs` registers recurring jobs when Hangfire is enabled.
- School leaderboard refresh runs every 10 minutes.
- Weekly snapshot runs hourly; monthly snapshot runs every six hours; anti-cheat ML review sweep runs every five minutes.
- Hangfire storage config uses `InvisibilityTimeout = 5 minutes`.

Potential failure:

```text
previous job run is slow or worker restarts -> next schedule overlaps or retries -> duplicate snapshots, duplicate anti-cheat work, or competing leaderboard updates.
```

Preventive action:

Add job idempotency/non-overlap specs and tests around job services: business keys, claimed rows, unique snapshot buckets, and re-run safety.

Follow-up prompt: `BACKEND2-CRIT-006`.

---

## Finding BACKEND2-CRIT-007 — Admin seeding/reset-on-start needs production guardrails

Severity: P1/P2  
Type: admin-seed-hardening / privileged bootstrap safety

Evidence from code:

- Admin seeding is enabled in Development or when `SeedAdmin:Enabled` is true.
- Default admin password is available only in Development, which is good.
- Existing admin password is reset on start when Development or `SeedAdmin:ResetPasswordOnStart` is true.
- In production, enabling `SeedAdmin:Enabled` plus reset behavior could create/reset privileged credentials on every start if not strongly guarded.

Potential failure:

```text
production deployment accidentally enables admin seeding/reset-on-start -> privileged account is created or password reset unexpectedly.
```

Preventive action:

Add production guardrails/tests: explicit emergency flag, no default password, one-time seed marker/audit, no password values in logs.

Follow-up prompt: `BACKEND2-CRIT-007`.

---

## Finding BACKEND2-CRIT-008 — Question draft/version numbering can race

Severity: P1/P2  
Type: authoring-version-race / content history integrity

Evidence from code:

- `SaveDraftAsync` reads latest draft for a question, then sets `DraftVersion = latest + 1` without an obvious transaction/unique retry around that calculation.
- `PublishAsync` reads latest `QuestionVersion`, then sets `VersionNumber = previous + 1` within a transaction, but no explicit unique constraint/retry is visible from the service code.
- Preview cache is updated while draft/version work is being saved; cache consistency depends on later save/commit success.

Potential failure:

```text
two concurrent save-draft or publish requests read the same latest version -> duplicate draft/version numbers or mismatched audit/cache pointers.
```

Preventive action:

Add concurrency tests and unique index/retry strategy for draft and published version numbering.

Follow-up prompt: `BACKEND2-CRIT-008`.

---

## New rules added

Created:

- `docs/BACKEND_SECOND_PASS_RISK_PREVENTION_RULES.md`

New mandatory themes:

- proxy trust boundary and rate-limit identity;
- refresh-token single-use rotation under concurrency;
- atomic/compensating registration;
- explicit content-author/admin policy for authoring mutations;
- adaptive answer input bounds;
- idempotent/non-overlapping recurring jobs;
- production-safe admin seeding;
- race-safe draft/version numbers.

---

## New prompt queue added (audit-created only)

Created prompt-ready queues — **not** runtime fixes:

- `docs/prompt_queues/backend_second_pass_risk_prevention.md`

Initial prompts:

1. `BACKEND2-CRIT-001` — proxy trust and rate-limit spoofing tests.
2. `BACKEND2-CRIT-002` — refresh-token rotation race hardening.
3. `BACKEND2-CRIT-003` — mobile registration atomicity.
4. `BACKEND2-CRIT-004` — question authoring authorization policy.
5. `BACKEND2-CRIT-005` — adaptive answer input bounds.
6. `BACKEND2-CRIT-006` — recurring job idempotency/non-overlap.
7. `BACKEND2-CRIT-007` — admin seeding production hardening.
8. `BACKEND2-CRIT-008` — question draft/version race safety.

---

## Recommended priority

Run first:

```text
BACKEND2-CRIT-004
BACKEND2-CRIT-001
BACKEND2-CRIT-002
BACKEND2-CRIT-003
```

Reason: these affect content integrity, security boundary, auth session safety, and account creation consistency.

Then run:

```text
BACKEND2-CRIT-005
BACKEND2-CRIT-006
BACKEND2-CRIT-007
BACKEND2-CRIT-008
```

Reason: important model/job/privileged-bootstrap/content-history hardening, but usually second after auth/security/content authorization.

---

## Residual risk

This audit did not execute backend tests and did not inspect every migration/index or background job service in depth. Treat it as a second-pass backend static risk audit that creates focused prompts, not as proof that every listed risk is a confirmed bug or fixed.

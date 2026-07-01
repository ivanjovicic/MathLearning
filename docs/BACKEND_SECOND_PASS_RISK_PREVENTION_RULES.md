# Backend Second-Pass Risk Prevention Rules

Last aligned: 2026-07-01  
Repo: `ivanjovicic/MathLearning`  
Scope: second-pass backend/API risk classes not covered by `BACKEND_CRITICAL_RISK_PREVENTION_RULES.md`

This document is a backend-specific second-pass guardrail addendum. It does not replace:

- `AGENTS.md`
- `docs/DOCS_INDEX.md`
- `docs/ARCHITECTURE_OVERVIEW.md`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/BACKEND_CHANGE_CHECKLIST.md`
- `docs/BACKEND_CRITICAL_RISK_PREVENTION_RULES.md`
- `docs/BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md`

Read it before changing backend flows related to:

- forwarded headers / reverse proxy behavior;
- rate limiting and client identity;
- refresh-token rotation;
- registration/login atomicity;
- question authoring / publish / draft versioning;
- adaptive answer input bounds;
- Hangfire recurring jobs;
- admin seeding and privileged bootstrap behavior.

---

## Backend second-pass prevention block

Every backend prompt touching these surfaces must include:

```text
Backend second-pass risk rules:
- Read docs/BACKEND_SECOND_PASS_RISK_PREVENTION_RULES.md before editing.
- Name the risk class: proxy-trust-boundary, refresh-token-rotation-race, auth-registration-atomicity, authoring-authorization, adaptive-input-bounds, recurring-job-overlap, admin-seed-hardening, or authoring-version-race.
- Do not trust X-Forwarded-* headers from arbitrary clients; configure known proxies/networks or prove the platform strips spoofed headers.
- Rate limiting must key off a trustworthy client identity and must not be bypassable by spoofed forwarded headers.
- Refresh-token rotation must be single-use under concurrent requests and must detect replay/reuse safely.
- Registration/login flows must not leave orphan Identity users, profiles, or refresh tokens after partial failures.
- Content authoring mutation routes must require an explicit admin/content-author policy, not generic authentication.
- Adaptive answer inputs must clamp or reject invalid confidence, response time, timestamp, and answer lengths.
- Recurring background jobs must be idempotent, non-overlapping where needed, and safe when a previous run is still active.
- Production/admin seeding must never silently reset privileged credentials or create/default privileged users without explicit safe configuration and audit logs.
- Draft/version numbers must be generated under transaction/unique constraints so concurrent saves/publishes cannot create duplicate version numbers.
- Add or update the smallest integration/contract/regression test for the exact risk class.
```

---

## Rule 1 — Proxy trust boundary and rate-limit identity

Risk:

```text
The API trusts caller-supplied X-Forwarded-* headers, then rate limiting keys by spoofable client IP.
```

Rules:

- In production, only trust forwarded headers from known reverse proxies/networks.
- If running behind Fly/edge proxy, document exact trust boundary.
- Rate limiter identity must use a trusted remote IP after proxy validation, or authenticated user id for authenticated routes.
- Host/scheme derived from forwarded headers must not affect security decisions unless trusted.

Minimum tests/review:

- spoofed `X-Forwarded-For` cannot create unlimited rate-limit buckets;
- known proxy header path still resolves expected client IP;
- production config fails or warns when forwarded header trust is unbounded;
- rate-limit tests cover anonymous and authenticated requests.

---

## Rule 2 — Refresh-token rotation must be concurrency-safe

Risk:

```text
Two concurrent refresh requests validate the same old refresh token before either SaveChanges call, then both mint new tokens.
```

Rules:

- Refresh token rotation must happen in a transaction or atomic conditional update.
- A token that is already revoked must not mint another token under race.
- Reuse of a revoked/rotated token should be detected and can revoke descendant tokens if product policy requires.
- Refresh responses must be safe and generic; do not leak token state details beyond invalid/expired.

Minimum tests:

- concurrent refresh with same token yields exactly one successful rotation;
- second concurrent/reuse attempt returns 401/invalid without creating another active token;
- revoked token cannot be used after logout/revoke-all;
- token row contains enough audit state for support without exposing token value in logs.

---

## Rule 3 — Auth registration must be atomic or compensating

Risk:

```text
Identity user is created, then profile or refresh token persistence fails, leaving an orphan or partially registered account.
```

Rules:

- Registration should use a transaction that covers Identity user, UserProfile, welcome economy state, and refresh token where feasible.
- If cross-store transaction is not feasible, add compensating cleanup for partial user/profile/token creation.
- Welcome bonus must not be double-granted under retry.
- Registration error response must be safe and generic while server logs keep correlation id.

Minimum tests:

- profile save failure does not leave orphan Identity user;
- refresh-token save failure does not leave user appearing fully registered without token;
- retry after partial failure does not double-grant welcome coins;
- duplicate username/email behavior remains stable.

---

## Rule 4 — Authoring routes need explicit content-author authorization

Risk:

```text
Any authenticated user can access question authoring mutation routes such as save-draft, publish, or revalidate.
```

Rules:

- Question/content authoring mutations must require Admin or a dedicated ContentAuthor policy.
- Validation/preview endpoints must also be classified: public-to-auth, content-author-only, or admin-only.
- If learners can validate/preview their own generated content, keep that separate from publish/save-draft routes.
- Actor user id must be recorded only after authorization succeeds.

Minimum tests:

- normal authenticated learner cannot publish, save draft, or revalidate content;
- admin/content-author can perform authorized authoring actions;
- unauthorized attempts leave no draft/version/audit rows;
- endpoint inventory documents exact policy.

---

## Rule 5 — Adaptive answer inputs must be bounded

Risk:

```text
Adaptive answer payload accepts unbounded confidence, response time, timestamp, or answer length and downstream scoring/model logic trusts it.
```

Rules:

- Clamp or reject confidence outside `[0, 1]`.
- Reject negative and unreasonable response times.
- Normalize `answeredAt` to UTC and bound future/past values.
- Limit answer string length and reject empty/huge payloads.
- Validation errors must be consistent `VALIDATION_ERROR` responses.

Minimum tests:

- confidence `-1`, `2`, `NaN`, and huge values are rejected/clamped as policy states;
- huge response time does not affect model/scoring unboundedly;
- future/past adaptive answer timestamps are bounded;
- oversized answer string is rejected.

---

## Rule 6 — Recurring jobs must be idempotent and non-overlapping

Risk:

```text
A frequent Hangfire job starts again while a previous run is still active, causing duplicate aggregation, duplicated review work, or competing leaderboard snapshots.
```

Rules:

- Every recurring job must declare idempotency key/scope and overlap policy.
- Snapshot jobs should use unique business keys such as period + snapshot time bucket.
- Sweep jobs should mark claimed work atomically before processing.
- If jobs run every 5 or 10 minutes, tests must cover slow previous run/overlap behavior.
- Disabled Hangfire mode must not make API responses claim background processing occurred.

Minimum tests/review:

- practice daily aggregation re-run for same day is idempotent;
- school leaderboard snapshot cannot duplicate same period/bucket;
- anti-cheat sweep claims rows once under concurrent workers;
- job registration names and schedules are documented.

---

## Rule 7 — Admin seeding and privileged bootstrap must be hardened

Risk:

```text
Production config enables admin seeding or reset-on-start, silently creating or resetting privileged credentials.
```

Rules:

- Admin seeding in production must require explicit username/password and an explicit production-safe flag.
- ResetPasswordOnStart must be development-only by default and strongly guarded outside development.
- Never use default admin password outside development.
- Log privileged bootstrap actions safely without password values.
- Consider one-time seed marker/audit record for production admin bootstrap.

Minimum tests/review:

- production cannot seed admin with default/missing password;
- production cannot reset admin password on every start unless explicit emergency flag is set;
- development behavior remains convenient but clearly separated;
- logs never contain seeded password.

---

## Rule 8 — Question draft/version numbering must be race-safe

Risk:

```text
Two concurrent save-draft or publish operations read the same latest version and both create the same next DraftVersion/VersionNumber.
```

Rules:

- DraftVersion and VersionNumber generation must be protected by transaction/unique index/retry.
- Concurrent publish must not create duplicate published versions for the same draft.
- Draft and version updates must be atomic with audit logs.
- Cache updates must not point to a draft/version that later fails to commit.

Minimum tests:

- concurrent save-draft for same question creates unique sequential draft versions;
- concurrent publish for same draft results in one publish or safe idempotent/conflict response;
- audit log and current draft/version pointer match committed version;
- preview cache is not left pointing at rolled-back content.

---

## Stop rules

Stop and create a narrower prompt instead of broad editing when:

- a fix changes auth/session/token semantics consumed by mobile;
- a proxy/rate-limit change depends on hosting provider behavior that is not documented;
- an authoring policy change might block admin UI workflows;
- a background job needs a new unique index/migration;
- a draft/version race fix needs schema changes;
- a production seed behavior change could lock out existing admin access.

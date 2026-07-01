# Backend Critical Risk Prevention Rules

Last aligned: 2026-07-01
Repo: `ivanjovicic/MathLearning`
Scope: backend/API risk classes for MathLearning mobile-facing flows

> **Prevention rules from static audit — not fix proof.**
> These rules describe required behavior for **future** implementation prompts (`BACKEND-CRIT-001..008`).
> They do not mean the risks are already fixed. Evidence of a fix requires runtime/test commit + `.ai/runs` log.

This document is a backend-specific guardrail addendum. It does not replace:

- `AGENTS.md`
- `docs/DOCS_INDEX.md`
- `docs/ARCHITECTURE_OVERVIEW.md`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/BACKEND_CHANGE_CHECKLIST.md`
- `docs/mobile_contract_idempotency_handoff.md`
- `docs/backend_contract_gap_report.md`

Read it before changing backend flows related to:

- auth/login/refresh/logout/register;
- global exception handling;
- monitoring/logging endpoints;
- public/profile/search/leaderboard surfaces;
- avatar/file upload and static file serving;
- quiz/offline answer ingestion;
- economy/season/cosmetics settlement;
- idempotency and replay behavior;
- request limits, timestamps, and pagination.

---

## Backend critical prevention block

Every backend prompt touching these surfaces must include:

```text
Backend critical risk rules:
- Read docs/BACKEND_CRITICAL_RISK_PREVENTION_RULES.md before editing.
- Name the risk class: backend-error-leak, monitoring-log-exposure, public-identity-minimization, avatar-upload-safety, settlement-snapshot-truth, mutation-idempotency-required, offline-time-trust, or unbounded-read-surface.
- Do not return raw exception messages, stack traces, database errors, SQL/provider errors, or Identity internal details to mobile clients.
- Do not expose monitoring/log-file endpoints publicly unless the payload is explicitly safe, redacted, and documented.
- Public/social/search/leaderboard DTOs must use a field allowlist and must not expose email, private profile details, private progress, parent data, raw user ids where avoidable, or detailed weakness data.
- File upload endpoints must validate max size, extension, MIME type, decoded content where feasible, storage path, and authenticated ownership.
- Response snapshots for settled mutations must reflect the mutation being returned; build response from persisted/tracked mutation state or save before no-tracking reads.
- Retryable mobile mutations must either require operationId/idempotencyKey or explicitly document legacy non-idempotent mode and keep it out of offline retry flows.
- Offline timestamps must be bounded, UTC-normalized, and safe for streak/anti-cheat/idempotency logic.
- Add or update the smallest integration/contract/regression test for the exact risk class.
```

---

## Rule 1 — Backend errors must be safe for mobile clients

Risk:

```text
An endpoint catch block or global exception response returns ex.Message or backend/Identity/provider details to the Flutter app.
```

Rules:

- User-facing responses must use safe error codes and generic localized-safe messages.
- Raw exception text belongs only in server logs with correlation id.
- `errorDetails` sent to clients must not include raw `ex.Message` in production-safe responses.
- Auth failures must not disclose whether username/email exists beyond intentional generic behavior.
- Identity validation errors may be mapped to safe codes; do not return arbitrary provider text unfiltered.

Minimum tests:

- login unexpected exception returns safe generic error and trace/correlation id, not raw message;
- refresh/logout/revoke-all unexpected exception does not return `ex.Message`;
- global exception response does not include raw database/provider message;
- 429 keeps `Retry-After` without leaking internals.

---

## Rule 2 — Monitoring/log endpoints must be protected or redacted

Risk:

```text
A public/internal monitoring endpoint exposes log lines, paths, exception messages, user identifiers, or operational details.
```

Rules:

- Log-reading endpoints must require admin/internal policy or be disabled outside development.
- If any observability route is anonymous, its payload must be explicitly safe and redacted.
- Do not expose filesystem paths, raw log lines, stack traces, SQL, tokens, emails, answers, request payloads, or exception bodies.
- `/metrics` should remain minimal and should not include user data or endpoint-level sensitive labels.

Minimum tests:

- non-admin cannot read log endpoints in production-like environment;
- log endpoint redacts tokens/emails/answers if it remains available;
- health/metrics endpoints return only safe operational data.

---

## Rule 3 — Public identity surfaces need a backend field allowlist

Risk:

```text
User search, public profile, leaderboard, school leaderboard, or rivals responses return too much child identity or progress data.
```

Rules:

- Define allowed public fields per surface before adding fields.
- Search results should be minimal and rate-limited; do not return email, private profile, parent data, or detailed progress.
- Public profile should not mirror private profile fields by default.
- Leaderboard should expose only fields needed for rank display.
- Use DTOs dedicated to public/social surfaces rather than anonymous projections that can grow accidentally.

Minimum tests:

- user search response does not include email/private fields;
- public profile response does not include daily/weekly/monthly private progress unless explicitly allowed;
- leaderboard item does not include email/parent/weak-area fields;
- query length and max limit are enforced.

---

## Rule 4 — Avatar/file upload must be treated as untrusted input

Risk:

```text
A legacy avatar upload accepts arbitrary file extension/content/size and stores it under a path that can be served publicly.
```

Rules:

- Require authentication and route/auth user match.
- Enforce max upload size.
- Allowlist extensions and MIME types.
- Sniff/validate decoded image content where feasible.
- Generate server-side filenames; never trust original filename except for extension after validation.
- Store outside public static paths or serve through an auth-checked endpoint.
- If static serving exists, prove uploaded private avatars cannot be fetched directly by guessing the file path.

Minimum tests:

- `.exe`, `.html`, `.svg` or unknown file types are rejected;
- over-size file is rejected;
- spoofed content-type is rejected if decoded content is invalid;
- user cannot fetch another user's avatar via legacy route;
- static `/uploads` route cannot bypass auth for protected avatar files, or this is documented as public-by-design.

---

## Rule 5 — Settlement response snapshots must reflect committed domain state

Risk:

```text
A mutation updates tracked entities, then builds a no-tracking response snapshot before SaveChanges/commit, returning stale earnedXp/claimedIds/inventory state.
```

Rules:

- Build settlement responses from the mutation result in memory, or save changes before no-tracking read models.
- Do not call no-tracking state builders before pending tracked changes are persisted unless the builder explicitly merges pending changes.
- Idempotency stored result must match what the client should replay forever.
- If a later local cache/reward sync step can fail, response must represent partial/retryable state honestly.

Minimum tests:

- season Daily Run claim response includes newly awarded XP;
- season milestone claim response includes the newly claimed milestone id and reward;
- replay returns the same settled body as the first response;
- commit failure cannot leave idempotency ledger completed with stale/success body.

---

## Rule 6 — Offline/mobile mutations must not accidentally bypass idempotency

Risk:

```text
A retryable mobile mutation works without operationId/idempotencyKey and falls back to legacy behavior, allowing duplicate work under offline replay or flaky networks.
```

Rules:

- P0 mobile mutations should require an operation identity unless a documented compatibility mode is intentionally allowed.
- Compatibility mode must not be used by offline queue/replay clients.
- Duplicate same payload must replay the same response.
- Same operation identity with different payload must conflict except for documented domain-policy exceptions like Daily Run chest Policy B.
- Legacy aliases must not silently drop invalid rows without a response diagnostic when used by mobile sync.

Minimum tests:

- P0 endpoint without operation identity is rejected or explicitly returns documented legacy behavior;
- mobile offline replay path requires stable operation/session identity;
- batch-submit invalid item handling is reported, not silently hidden, if used as mobile contract;
- conflict response is stable and does not mutate domain state.

---

## Rule 7 — Offline timestamps must be bounded and normalized

Risk:

```text
Offline answers with far-future or far-past timestamps affect streak, daily limits, anti-cheat, idempotency keys, or reporting incorrectly.
```

Rules:

- Normalize incoming timestamps to UTC.
- Reject or quarantine timestamps that are too far in the future.
- Define acceptable past window for offline replay.
- Streak/daily calculations must use a documented calendar authority: UTC or user-local day.
- Idempotency/duplicate keys should not rely only on raw client timestamp precision where operation ids exist.

Minimum tests:

- future offline answer is rejected/quarantined;
- very old offline answer does not advance today's streak unexpectedly;
- equivalent timestamp formats normalize to the same UTC instant;
- duplicate answer replay does not depend on local timezone string representation.

---

## Rule 8 — Read surfaces need bounded query limits and safe defaults

Risk:

```text
Search, leaderboard, history, monitoring, or admin endpoints accept unbounded limit/take/pageSize or expensive query parameters.
```

Rules:

- Clamp all `limit`, `take`, and `pageSize` values to documented maximums.
- Normalize periods/scopes/ranges to allowed values.
- Reject negative/zero values consistently.
- User-facing search should enforce minimum query length and reasonable max limit.
- Admin/internal reads can be larger only behind admin policy and explicit caps.

Minimum tests:

- user search clamps limit;
- leaderboard period/scope invalid values are rejected or normalized safely;
- school history `take` is capped;
- log/monitoring endpoints cannot return unbounded raw lines.

---

## Stop rules

Stop and create a narrower prompt instead of editing broadly when:

- an endpoint mixes auth/user resolution, mutation, idempotency, and response shaping in a way that needs tests first;
- a response DTO is shared between private and public surfaces;
- a file upload path is served by both static files and an auth-gated route;
- an idempotency behavior change could alter stored replay semantics;
- a migration or index change could affect already-settled ledgers;
- a fix would require a mobile contract change that is not documented in both repos.

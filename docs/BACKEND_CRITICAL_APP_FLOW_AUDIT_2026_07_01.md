# Backend Critical App Flow Audit — 2026-07-01

Status: static backend repo/code audit  
Repo: `ivanjovicic/MathLearning`  
Scope: backend/API flows consumed by `ivanjovicic/Mathlearning-Mobile-App`  
Validation: docs/code inspection only through GitHub connector; no `dotnet test` executed in this audit

## Executive verdict

The backend already has strong architecture docs and a clear P0 focus on mobile/backend contract parity and idempotent settlement. The existing docs correctly identify P0 endpoints and idempotency scopes for quiz, SRS, Daily Run, seasons, economy, cosmetics, shop, and rewards.

This audit found additional backend risk classes that are not just mobile-side risks:

```text
raw exception messages can still reach clients from endpoint catch blocks and global exception errorDetails
monitoring/log endpoints expose raw log-like data without clear admin/internal enforcement
public/search/leaderboard/profile DTOs need explicit child/public identity allowlists
legacy avatar upload lacks obvious file size/type/content validation and static uploads may bypass auth
settlement responses may be built from no-tracking read models before pending tracked changes are persisted
retryable mobile mutations can still fall back to non-idempotent legacy behavior when operation keys are absent
offline answer timestamps can affect streak/anti-cheat/idempotency if not bounded and normalized
read endpoints need consistent limit/period/scope caps
```

These are potential risks, not confirmed production incidents.

---

## Finding BACKEND-CRIT-001 — Raw exception/error details can leak to clients

Severity: P1  
Type: backend-error-leak / security / client trust

Evidence from code:

- `AuthEndpoints.LoginHandler` catches `Exception ex` and returns `new { error = ex.Message }` with status 500.
- Refresh/logout/revoke-all catch blocks also return `ex.Message`.
- `GlobalExceptionMiddleware` returns `errorDetails` containing `exceptionType` and `ex.Message` for both 500 and rate-limited paths.

Potential failure:

```text
DB/provider/Identity/internal exception occurs -> mobile receives raw exception text or internal backend detail.
```

Preventive action:

Add safe backend error response rules and tests. Raw exception text should stay in logs with correlation/trace id, not in mobile JSON.

Follow-up prompt: `BACKEND-CRIT-001`.

---

## Finding BACKEND-CRIT-002 — Monitoring/log endpoints may expose operational details

Severity: P1/P2  
Type: monitoring-log-exposure / operational security

Evidence from code/docs:

- API inventory marks `/api/monitoring/jobs`, `/api/monitoring/logs`, and `/api/monitoring/logs-advanced` as `Public/internal`.
- `Program.cs` maps monitoring/log endpoints directly and reads log lines from a file path when present.
- These endpoints are not obviously behind admin policy in the mapping shown.

Potential failure:

```text
non-admin caller can read log-like operational output, filesystem paths, exception text, or future sensitive log lines.
```

Preventive action:

Guard log-reading endpoints behind admin/internal policy or redact/disable outside Development. Add tests for anonymous/non-admin access.

Follow-up prompt: `BACKEND-CRIT-002`.

---

## Finding BACKEND-CRIT-003 — Public/search/leaderboard identity minimization is under-specified

Severity: P1/P2  
Type: public-identity-minimization / child privacy

Evidence from code:

- `GET /api/users/search` returns `UserId`, `Username`, `DisplayName`, `Level`, and `Xp` plus appearance.
- `GetProfileByIdAsync` returns `UserId`, `Username`, `Xp`, `Level`, `Streak`, `DailyXp`, `WeeklyXp`, `MonthlyXp`, `AvatarUrl`, and appearance.
- Leaderboard endpoints return `UserId`, display name, score/XP, streak, level, and appearance.

Potential failure:

```text
public/social surface exposes more child identity or progress detail than the mobile UI actually needs.
```

Preventive action:

Add backend public identity allowlist and tests for user search, public profile, leaderboard, rivals, and school surfaces.

Follow-up prompt: `BACKEND-CRIT-003`.

---

## Finding BACKEND-CRIT-004 — Legacy avatar upload/static file serving risk

Severity: P1/P2  
Type: avatar-upload-safety / file upload security / privacy

Evidence from code:

- Legacy avatar upload reads the first form file, checks only missing/zero length, uses `Path.GetExtension(file.FileName)`, generates a server filename, and writes to `uploads/avatars`.
- Legacy avatar read route checks route user ownership and the profile URL suffix.
- `Program.cs` also serves physical `uploads` directory publicly under `/uploads` with static files.

Potential failures:

```text
unsupported file type or oversized file is stored.
public /uploads path bypasses the auth-gated /users/{id}/avatar/{fileName} route if file name is guessed or leaked.
```

Preventive action:

Add upload file size/type/content validation and decide whether uploaded avatars are public-by-design or must be served only through auth-gated routes.

Follow-up prompt: `BACKEND-CRIT-004`.

---

## Finding BACKEND-CRIT-005 — Settlement responses can be stale if built before persistence

Severity: P1  
Type: settlement-snapshot-truth / idempotency replay correctness

Evidence from code:

- Season Daily Run claim mutates `UserSeasonProgress`, adds `UserSeasonDailyRunClaim`, then calls `BuildSeasonStateAsync` before `txService.CompleteAsync` and transaction commit.
- Season milestone claim mutates profile/cosmetic/fragment state, adds `UserSeasonMilestoneClaim`, then calls `BuildSeasonStateAsync` before idempotency completion/commit.
- `BuildSeasonStateAsync` uses `AsNoTracking()` database queries for progress and claimed ids.

Potential failure:

```text
first settlement response or stored idempotency replay body does not include the mutation that is being settled.
```

Preventive action:

Add tests proving settlement responses include newly awarded XP/claimed milestone and replay the same body. If needed, build response from tracked mutation state or save before no-tracking reads.

Follow-up prompt: `BACKEND-CRIT-005`.

---

## Finding BACKEND-CRIT-006 — Retryable mobile mutations can bypass idempotency when keys are absent

Severity: P1/P2  
Type: mutation-idempotency-required / duplicate replay

Evidence from code/docs:

- README and gap report define P0 endpoints and idempotency scope.
- `POST /api/quiz/answer` uses ledger only when operation keys are present; otherwise it falls back to legacy behavior.
- `/api/quiz/batch-submit` legacy alias builds an offline request and silently skips invalid answer rows while adapting payload.

Potential failure:

```text
mobile/offline retry path accidentally omits operationId/idempotencyKey -> backend processes legacy non-idempotent mutation path or hides skipped rows.
```

Preventive action:

Add a prompt to decide which P0 mobile mutations must reject missing operation identity and to test legacy aliases cannot be used by mobile offline replay without diagnostics.

Follow-up prompt: `BACKEND-CRIT-006`.

---

## Finding BACKEND-CRIT-007 — Offline timestamps need stronger bounds and normalization tests

Severity: P1/P2  
Type: offline-time-trust / streak and anti-cheat correctness

Evidence from code:

- Offline batch submit accepts `answeredAt` from client payload; if parse fails it falls back to `DateTime.UtcNow`.
- Deduplication key is `questionId:answeredAt.Ticks`.
- Anti-cheat rejects future offline answers only when `AnsweredAtUtc > DateTime.UtcNow.AddMinutes(2)`; older backdated timestamps are not obviously bounded in the endpoint flow.
- Streak overview is calculated from UTC day behavior elsewhere.

Potential failure:

```text
offline replay with very old, malformed, local-time, or precision-shifted timestamps affects duplicate detection, streaks, daily stats, or anti-cheat unexpectedly.
```

Preventive action:

Add UTC normalization, acceptable offline replay window, and tests for future/old/malformed/equivalent timestamp payloads.

Follow-up prompt: `BACKEND-CRIT-007`.

---

## Finding BACKEND-CRIT-008 — Read endpoints need consistent bounds and enum validation

Severity: P2  
Type: unbounded-read-surface / performance / abuse control

Evidence from code:

- User search takes `limit = 10` but does not obviously clamp max before `Take(limit)`.
- Some leaderboard endpoints clamp `limit`, while others pass `period`, `scope`, `limit`, `take`, and cursor-like values through service layers.
- Monitoring logs endpoints read recent lines, but public/internal status means bounds and access should be explicit.

Potential failure:

```text
large or invalid query params create expensive queries, inconsistent mobile behavior, or ambiguous invalid period/scope responses.
```

Preventive action:

Add tests/spec for max limits, allowed `period/scope/range`, negative values, and safe defaults across search/leaderboard/history/monitoring reads.

Follow-up prompt: `BACKEND-CRIT-008`.

---

## New rules added

Created:

- `docs/BACKEND_CRITICAL_RISK_PREVENTION_RULES.md`

New mandatory themes:

- safe backend error responses;
- protected/redacted monitoring and logs;
- public identity field allowlists;
- avatar upload validation and auth-safe serving;
- settlement response snapshot truth;
- mandatory idempotency for retryable mobile mutations;
- offline timestamp bounds;
- bounded read/query surfaces.

---

## New prompt queue added

Created:

- `docs/prompt_queues/backend_critical_risk_prevention.md`

Initial prompts:

1. `BACKEND-CRIT-001` — safe error response hardening.
2. `BACKEND-CRIT-002` — monitoring/log endpoint access and redaction.
3. `BACKEND-CRIT-003` — public identity minimization for search/profile/leaderboard.
4. `BACKEND-CRIT-004` — avatar upload and static serving safety.
5. `BACKEND-CRIT-005` — settlement response snapshot truth.
6. `BACKEND-CRIT-006` — required idempotency for retryable mobile mutations.
7. `BACKEND-CRIT-007` — offline timestamp bounds and UTC normalization.
8. `BACKEND-CRIT-008` — bounded read surfaces and enum validation.

---

## Recommended priority

Run first:

```text
BACKEND-CRIT-001
BACKEND-CRIT-002
BACKEND-CRIT-004
BACKEND-CRIT-005
```

Reason: these affect security/error leakage, operational data exposure, file upload safety, and idempotent settlement truth.

Then run:

```text
BACKEND-CRIT-003
BACKEND-CRIT-006
BACKEND-CRIT-007
BACKEND-CRIT-008
```

Reason: these are important privacy/contract/performance hardening items and should be done before production/beta claims.

---

## Residual risk

This audit did not execute backend tests and did not inspect every service/migration in depth. Treat it as a backend static risk audit that creates focused prompts, not as proof that every listed risk is a confirmed bug or fixed.

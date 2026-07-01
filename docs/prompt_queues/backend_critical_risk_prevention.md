# Backend Critical Risk Prevention Prompt Queue

Last aligned: 2026-07-01  
Target repo: `ivanjovicic/MathLearning`  
Lane: backend/API risk prevention for MathLearning mobile-facing flows  
Owns: backend error safety, monitoring/log exposure, public identity minimization, avatar upload safety, settlement response truth, idempotency requirements, offline timestamp bounds, bounded read surfaces  
Avoids: mobile repo runtime changes, broad endpoint rewrites, schema changes without migration/test evidence

Read first:

- `../AGENTS.md`
- `../DOCS_INDEX.md`
- `../ARCHITECTURE_OVERVIEW.md`
- `../API_ENDPOINT_INVENTORY.md`
- `../BACKEND_CHANGE_CHECKLIST.md`
- `../COMMON_AGENT_PITFALLS.md`
- `../mobile_contract_idempotency_handoff.md`
- `../backend_contract_gap_report.md`
- `../BACKEND_CRITICAL_RISK_PREVENTION_RULES.md`
- `../BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md`
- `../AGENT_RUN_LOG_ENFORCEMENT.md`
- `../ai/learning/MISTAKE_LEDGER.md`

Hard rules:

```text
Do not return raw ex.Message, SQL/provider errors, stack traces, or Identity internals to mobile clients.
Do not expose log-reading endpoints publicly unless redacted and explicitly documented as safe.
Do not add fields to public/search/leaderboard/profile DTOs without an allowlist and tests.
Do not accept avatar/file uploads without max size, type, content, path, and ownership validation.
Do not build no-tracking settlement response snapshots before the mutation they describe is persisted or merged.
Do not allow offline/retryable mobile mutations to use non-idempotent legacy paths without an explicit contract decision.
Do not trust client timestamps without UTC normalization and bounds.
Every Done row must name the backend risk prevented and exact integration/contract test added.
```

---

## Queue status model

| Status | Meaning |
|---|---|
| **Audit-created** | Finding documented in `BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md`; no runtime work started. |
| **Prompt-ready** | Implementation/spec prompt exists below; safe to pick when prerequisites are met. |
| **Runtime-fixed** | `src/**` or `tests/**` changed under this prompt ID (commit required). |
| **Validated** | `dotnet test` (or documented equivalent) passed and recorded in `.ai/runs` evidence. |

Creating the audit doc or this queue file is **audit-created / prompt-ready** work only. It is **not** `Runtime-fixed` or `Validated`.

### Done requirements

```text
Done requires: runtime and/or test commit + `.ai/runs/<date>-<prompt-id>-evidence.md` + validation result recorded.
Docs-only spec/audit work: max Done 85%; must not claim runtime fix.
Audit-created rows stay Prompt-ready until an implementation prompt completes with evidence.
```

### Recommended execution order

Run **security / auth / data-loss** risks first:

```text
BACKEND-CRIT-001 → BACKEND-CRIT-002 → BACKEND-CRIT-004 → BACKEND-CRIT-005
```

Then **privacy / public identity**:

```text
BACKEND-CRIT-003 → BACKEND-CRIT-008
```

Then **contract bounds / docs-first specs** (may be spec + follow-up implementation):

```text
BACKEND-CRIT-006 → BACKEND-CRIT-007
```

`BACKEND-CRIT-006` should follow `BACKEND-CRIT-005` evidence when settlement truth affects idempotency decisions.

---

## Active prompts

| ID | Status | Can run in parallel with | Purpose |
|---|---|---|---|
| BACKEND-CRIT-001 | Done 90% (uncommitted, 2026-06-24) | — | Harden backend error responses so raw exception messages do not reach clients. Run log: `.ai/runs/2026-06-24-BACKEND-CRIT-001-evidence.md`. Tests: `GlobalExceptionMiddlewareTests`, `AuthSafeErrorResponseTests`. Risk: backend-error-leak on auth + global middleware. |
| BACKEND-CRIT-002 | Done 90% (uncommitted, 2026-06-24) | — | Protect/redact monitoring/log endpoints. Run log: `.ai/runs/2026-06-24-BACKEND-CRIT-002-evidence.md`. Tests: `MonitoringLogAuthorizationTests`, `LogOutputRedactorTests`. Risk: monitoring-log-exposure. |
| BACKEND-CRIT-003 | Prompt-ready | BACKEND-CRIT-008 | Add public identity allowlist for search/profile/leaderboard DTOs. |
| BACKEND-CRIT-004 | Done (`95156ed`, 2026-06-24) | — | Harden legacy avatar upload and static file serving safety. Run log: `.ai/runs/2026-06-24-BACKEND-CRIT-004-evidence.md`. Tests: `LegacyAvatarUploadSafetyTests`. Risk: avatar-upload-safety. |
| BACKEND-CRIT-005 | Done (`b11f083`, 2026-06-24) | — | Settlement response snapshot truth for season daily-run and milestone claims. Run log: `.ai/runs/2026-06-24-BACKEND-CRIT-005-evidence.md`. Tests: `EconomySettlementEndpointsIntegrationTests`, `MobileEconomyContractIntegrationTests`. Risk: settlement-snapshot-truth. |
| BACKEND-CRIT-006 | Done 85% (2026-07-01, docs/spec, commit `1e53f1c`) | evidence lint only | Decide/enforce idempotency requirements for retryable mobile mutations. Run log: `.ai/runs/2026-07-01-BACKEND-CRIT-006-evidence.md`. Tests: docs-only validation. Risk: legacy no-key compatibility remains until a migration prompt hardens the mobile contract. |
| BACKEND-CRIT-007 | Prompt-ready | BACKEND-CRIT-006 | Add offline timestamp bounds and UTC normalization tests. |
| BACKEND-CRIT-008 | Prompt-ready | BACKEND-CRIT-003 | Clamp read endpoint limits and validate period/scope/range values. |

---

## BACKEND-CRIT-001 — Safe backend error response hardening

Run mode: implementation/test  
Token budget: medium

Task: prevent raw backend exception messages from reaching mobile clients.

Backend critical risk rules:

- Risk class: backend-error-leak.
- Raw exception text belongs in server logs only.
- Client responses should use safe error codes/messages plus trace/correlation id.

Before editing, inspect:

- `docs/BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md`
- `docs/BACKEND_CRITICAL_RISK_PREVENTION_RULES.md`
- `src/MathLearning.Api/Middleware/GlobalExceptionMiddleware.cs`
- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- existing auth/error middleware tests

Owned paths:

- `src/MathLearning.Api/Middleware/GlobalExceptionMiddleware.cs`
- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- targeted backend tests
- this queue status row only

Avoid paths:

- broad auth redesign
- refresh-token schema changes
- mobile repo changes

Required work:

1. Add tests proving login/refresh/logout/revoke-all unexpected exceptions do not return raw `ex.Message`.
2. Add tests proving global 500 errorDetails do not include raw database/provider message.
3. Preserve trace/correlation id for support.
4. Preserve 429 `Retry-After` semantics without leaking internals.
5. Keep detailed exception logs server-side only.

Validation:

```bash
dotnet test --filter "Auth|GlobalException|ErrorResponse"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND-CRIT-001-evidence.md` with validation command + result and commit SHA.
- Queue row may move to Done only after runtime/test commit and run log.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001

---

## BACKEND-CRIT-002 — Monitoring/log endpoint access and redaction

Run mode: implementation/test  
Token budget: medium

Task: ensure monitoring/log-reading endpoints are admin/internal only or safe/redacted outside Development.

Backend critical risk rules:

- Risk class: monitoring-log-exposure.
- Do not expose raw log lines, paths, stack traces, tokens, emails, answers, SQL, or exception bodies to anonymous users.

Before editing, inspect:

- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Endpoints/LoggingEndpoints.cs`
- `docs/API_ENDPOINT_INVENTORY.md`
- observability/monitoring tests

Owned paths:

- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Endpoints/LoggingEndpoints.cs` if relevant
- targeted endpoint tests
- docs inventory row if auth behavior changes
- this queue status row only

Required work:

1. Add tests proving anonymous/non-admin cannot read `/api/monitoring/logs` and `/api/monitoring/logs-advanced` in production-like environment, or prove they are disabled/redacted.
2. Keep `/health` and minimal `/metrics` safe.
3. Redact any log output if endpoint remains available.
4. Update `docs/API_ENDPOINT_INVENTORY.md` if auth/status changes.

Validation:

```bash
dotnet test --filter "Monitoring|Logging|Authorization"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND-CRIT-002-evidence.md` with validation + commit SHA.
- Update `docs/API_ENDPOINT_INVENTORY.md` auth rows if endpoint policy changes.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001

---

## BACKEND-CRIT-003 — Public identity minimization for search/profile/leaderboard

Run mode: docs/spec first, implementation/test for obvious leaks  
Token budget: medium

Task: define and enforce a backend allowlist for child/public identity fields returned by user search, public profile, leaderboard, rivals, and school leaderboard surfaces.

Backend critical risk rules:

- Risk class: public-identity-minimization.
- Public/social/search/leaderboard DTOs must use a field allowlist.
- Do not expose email, private profile details, parent data, detailed progress, or weak-area data.

Before editing, inspect:

- `src/MathLearning.Api/Endpoints/UserEndpoints.cs`
- `src/MathLearning.Api/Endpoints/LeaderboardEndpoints.cs`
- `src/MathLearning.Application/DTOs/Leaderboard/*`
- user/leaderboard endpoint tests
- mobile public identity docs if present

Owned paths:

- `docs/PUBLIC_IDENTITY_BACKEND_ALLOWLIST.md`
- `src/MathLearning.Api/Endpoints/UserEndpoints.cs` if DTO trimming is clearly needed
- `src/MathLearning.Api/Endpoints/LeaderboardEndpoints.cs` if DTO trimming is clearly needed
- targeted contract tests
- this queue status row only

Required work:

1. Define allowed fields for search, public profile, leaderboard, rivals, and school leaderboard.
2. Add tests proving forbidden fields are absent.
3. Decide whether XP/streak/daily/weekly/monthly values are allowed per surface.
4. Clamp/validate search query and limit if not covered by BACKEND-CRIT-008.
5. Do not change mobile contract without updating both repos.

Validation:

```bash
dotnet test --filter "UserSearch|PublicProfile|Leaderboard|Contract"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND-CRIT-003-evidence.md`.
- If spec-only first: cap Done at 85%; `docs/PUBLIC_IDENTITY_BACKEND_ALLOWLIST.md` does not prove DTO trimming landed.
- Record cross-repo sync if mobile public fields change.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001

---

## BACKEND-CRIT-004 — Avatar upload and static serving safety

Run mode: implementation/test  
Token budget: medium

Task: harden legacy avatar upload and ensure static file serving cannot bypass intended auth/ownership checks.

Backend critical risk rules:

- Risk class: avatar-upload-safety.
- File uploads are untrusted input.
- Validate max size, extension, MIME type, decoded image content where feasible, storage path, and authenticated ownership.

Before editing, inspect:

- `src/MathLearning.Api/Endpoints/UserEndpoints.cs`
- `src/MathLearning.Api/Program.cs`
- any avatar upload tests
- canonical cosmetics avatar endpoints so legacy route is not expanded accidentally

Owned paths:

- `src/MathLearning.Api/Endpoints/UserEndpoints.cs`
- `src/MathLearning.Api/Program.cs` only if static serving needs gating/narrowing
- targeted upload/static file tests
- docs inventory if route behavior changes
- this queue status row only

Required work:

1. Add tests rejecting unsupported extension/type and oversized file.
2. Add test rejecting spoofed content-type when content is invalid if feasible.
3. Add test proving user cannot fetch another user's avatar via legacy route.
4. Decide whether `/uploads/avatars/*` is public-by-design or must be blocked/moved/auth-gated.
5. Do not expand legacy route for new mobile runtime.

Validation:

```bash
dotnet test --filter "Avatar|Upload|StaticFiles|Users"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND-CRIT-004-evidence.md` with upload/static-file tests recorded.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001

---

## BACKEND-CRIT-005 — Settlement response snapshot truth

Run mode: implementation/test  
Token budget: medium/high

Task: prove and fix season/economy settlement responses so first response and replay body include the mutation being settled.

Backend critical risk rules:

- Risk class: settlement-snapshot-truth.
- Response snapshots for settled mutations must reflect the mutation being returned.
- Idempotency stored result must match what the client should replay forever.

Before editing, inspect:

- `src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs`
- `tests/MathLearning.Tests/Contracts/*`
- `tests/MathLearning.Tests/Idempotency/*`
- `docs/backend_contract_gap_report.md`

Owned paths:

- `src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs`
- targeted season/economy idempotency and contract tests
- docs gap report if evidence changes
- this queue status row only

Required work:

1. Add test: `/api/seasons/daily-run-claim` first response includes newly awarded season XP.
2. Add test: duplicate replay returns same settled body or documented already-claimed body with correct state.
3. Add test: milestone claim response includes newly claimed milestone id/reward.
4. Ensure no no-tracking read model misses pending tracked changes.
5. Ensure ledger completion cannot commit stale success body if DB commit fails.

Validation:

```bash
dotnet test --filter "SeasonDailyRunClaim|SeasonMilestone|Economy|Idempotency|Contract"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND-CRIT-005-evidence.md` with settlement/idempotency test proof.
- Update `docs/backend_contract_gap_report.md` only with test-backed evidence.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001

---

## BACKEND-CRIT-006 — Required idempotency for retryable mobile mutations

Run mode: investigation/spec first, implementation only after decision  
Token budget: medium

Task: decide and enforce which mobile P0 mutations must reject missing operation identity instead of falling back to legacy non-idempotent behavior.

Backend critical risk rules:

- Risk class: mutation-idempotency-required.
- Retryable mobile mutations must either require operation identity or explicitly document legacy non-idempotent mode.
- Offline/replay clients must not use compatibility modes that hide duplicates or skipped rows.

Before editing, inspect:

- `docs/mobile_contract_idempotency_handoff.md`
- `docs/backend_contract_gap_report.md`
- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- economy/cosmetics/Daily Run endpoints
- mobile repo contract docs only if needed for cross-repo status

Owned paths:

- `docs/MOBILE_MUTATION_IDEMPOTENCY_REQUIREMENTS_2026_07_01.md`
- targeted endpoint tests if requirement is already clear
- endpoint code only if docs/tests prove safe behavior
- this queue status row only

Required work:

1. List all P0 retryable mobile mutations and whether operation identity is required today.
2. Decide whether `/api/quiz/answer` should reject missing operation identity for mobile contract mode.
3. Decide whether legacy `/api/quiz/batch-submit` should return per-item diagnostics instead of silently skipping invalid rows.
4. Add follow-up implementation prompts for any contract-changing behavior.
5. Do not break documented legacy compatibility without explicit migration plan.

Validation:

```bash
git diff --check
dotnet test --filter "Idempotency|MobileMutationContract"  # if runtime/tests changed
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND-CRIT-006-evidence.md`.
- Spec doc `docs/MOBILE_MUTATION_IDEMPOTENCY_REQUIREMENTS_2026_07_01.md` alone = audit/spec (≤85%); implementation prompts required for contract changes.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001

---

## BACKEND-CRIT-007 — Offline timestamp bounds and UTC normalization

Run mode: implementation/test or spec-first if product policy is unclear  
Token budget: medium

Task: make offline answer timestamps safe for streak, anti-cheat, duplicate detection, and replay.

Backend critical risk rules:

- Risk class: offline-time-trust.
- Offline timestamps must be bounded, UTC-normalized, and safe for streak/anti-cheat/idempotency logic.

Before editing, inspect:

- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- `StreakRoller` implementation
- offline submit tests
- mobile offline contract docs

Owned paths:

- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- targeted offline submit/streak/anti-cheat tests
- optional docs note for accepted offline replay window
- this queue status row only

Required work:

1. Add tests for future timestamp, very old timestamp, malformed timestamp, local offset timestamp, and duplicate precision variants.
2. Define accepted offline replay window.
3. Normalize incoming `answeredAt` to UTC.
4. Ensure malformed timestamp does not silently become `DateTime.UtcNow` without diagnostic if used for contract replay.
5. Ensure streak/daily logic follows documented calendar authority.

Validation:

```bash
dotnet test --filter "OfflineSubmit|Timestamp|Streak|AntiCheat"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND-CRIT-007-evidence.md` with offline timestamp test matrix recorded.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001

---

## BACKEND-CRIT-008 — Bounded read surfaces and enum validation

Run mode: implementation/test  
Token budget: medium

Task: clamp limits and validate allowed query values for user search, leaderboard, school history, monitoring reads, and similar read-heavy endpoints.

Backend critical risk rules:

- Risk class: unbounded-read-surface.
- Clamp `limit`, `take`, and `pageSize`; validate period/scope/range; reject or normalize unsafe values.

Before editing, inspect:

- `src/MathLearning.Api/Endpoints/UserEndpoints.cs`
- `src/MathLearning.Api/Endpoints/LeaderboardEndpoints.cs`
- `src/MathLearning.Api/Program.cs` monitoring endpoints
- endpoint tests

Owned paths:

- targeted endpoint code/tests
- docs inventory if semantics change
- this queue status row only

Required work:

1. Clamp user search `limit` to a documented maximum.
2. Validate or normalize leaderboard `scope`, `period`, and legacy `range` values.
3. Clamp school history `take` and neighbors.
4. Ensure negative/zero values do not produce ambiguous behavior.
5. Ensure monitoring reads cannot return unbounded raw lines.

Validation:

```bash
dotnet test --filter "UserSearch|Leaderboard|Monitoring|Bounds|Validation"
```

Evidence output requirement:

- `.ai/runs/<yyyy-mm-dd>-BACKEND-CRIT-008-evidence.md` with clamp/validation tests recorded.

Relevant prior mistakes read:

- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001

---

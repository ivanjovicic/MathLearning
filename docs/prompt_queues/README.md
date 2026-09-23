# Backend Prompt Queue Router

Last aligned: 2026-09-21
Owner: `backend-agent-system`

Current code and executed tests override old queue prose. Completed archives and main-verified run evidence override stale Ready rows.

## 2026-09-23 PR reconciliation

The 2026-09-23 refresh package re-audited current `main`, ported only residuals into fresh PRs, merged them, and closed the stale implementation PRs. Current residuals and provider-validation caps are recorded in the canonical rows and run logs below.

- `BACKEND-API-DB-015`: old PRs #16/#17 superseded; current-main lease implementation is delivered, with PostgreSQL proof residual.
- `BACKEND-TEST-049`: old PR #19 superseded by fresh PR #35, merged to `main`.
- `BACKEND-TEST-050`: old PR #20 superseded by fresh PR #36, merged to `main`.
- `BACKEND-TEST-051`: old PR #21 superseded by fresh PR #37, merged to `main`.
- `BACKEND-TEST-026`: old PR #23 superseded by fresh PR #38, merged to `main`.
- `BACKEND-TEST-033`: old PR #24 superseded; practice proof remains on current `main`, with broader provider cells residual.
- season/economy contract PR #25 superseded by fresh PR #39, merged to `main`.
- `BACKEND-TEST-027`: Done 95% via PR #33 / `a6cf0430`; dead endpoint removed and scoped validation clean. The repository-wide 12-test Database Validation baseline is tracked separately and must not reopen this owner.

Superseded PRs #13, #14 and #15 are closed because their canonical BE-PERF owners are already Done on current main. PR #31 is merged; `BACKEND-OPS-DB-IDLE-BUDGET-001` is closed at 95% with provider-workflow/live-idle residuals recorded separately.

## Start rule

1. If the user assigned a bounded task, do it directly with `Queue: user-assigned`; no queue search/admission.
2. If the user asked for next work or named a queue, inspect this router and one highest-priority non-blocked row.
3. Refresh visible branch/PR ownership for that row only.
4. Create/promote a formal active prompt only through v2/v3 admission.
5. Do not reopen archived IDs; use one new residual ID.

## Current canonical priority

| Order | Queue/owner | Remaining ownership |
|---:|---|---|
| 1 | `backend_db_cost_guardrails_2026_09_21.md` | P0 pre-production cost guardrails: Fly auto-stop plus explicit DB idle/background-work budget so serverless Postgres can genuinely idle. |
| 2 | `backend_registration_ops_residuals_2026_09_09.md` | Neon/Fly host starvation: bounded health/liveness and Hangfire/outbox outage isolation found during registration incident work. |
| 3 | `backend_auth_session_residuals_2026_09_18.md` | Refresh/logout HTTP contract: stable refresh failures plus idempotent, non-disclosing refresh-token revocation for mobile logout. |
| 4 | `backend_season_authority_residuals_2026_07_31.md` | Newly confirmed season authority defects: reward-track lifetime-XP/inactive/premium bypass, cross-season Daily Run chest provenance and milestone XP accounting drift. |
| 5 | `backend_cross_repo_current_main_2026_07_17.md` | Adaptive session-start idempotency, durable private bug screenshots and stale PR #3 disposition. |
| 6 | `backend_performance_followups_2026_07_03.md` | Existing P0 adaptive answer/practice settlement owners `BE-PERF-012` and `BE-PERF-015`, plus bounded limiter owner `BE-PERF-011`. |
| 7 | `backend_test_coverage.md` | Provider/cancellation/regression proof that supports canonical runtime owners without reimplementing them; top unresolved bugs are `BACKEND-API-DB-015`, `BACKEND-TEST-033` and the newly surfaced `BACKEND-TEST-049`/`BACKEND-TEST-050`/`BACKEND-TEST-051`. `BACKEND-API-DB-013` runtime slice delivered (historical orphan backfill deferred). |
| 8 | `backend_code_analysis.md` | Scheduled/manual free analyzer, dependency and risk-pattern audit; route unique findings through `BACKEND-ANALYSIS-001`. |
| 9 | `backend_api_db_residuals_pass3_2026_07_16.md` | Historical pass-3 evidence; `016` is superseded by `020`, `017..019` are delivered/nonclaimable. |
| 10 | `backend_failing_test_followups_2026_07_11.md` | Historical migration repair; no active row after `BACKEND-MIGRATION-001` delivery. |

`backend_critical_risk_prevention.md` and earlier pass queues are historical evidence. Their completed rows are not active selection sources.

## Existing-owner cross-repo routing

| Flutter/backend need | Backend owner | Action |
|---|---|---|
| Season reward-track unlock/claim authority | `BACKEND-SEASON-TRACK-AUTHORITY-001` | Use persisted season XP and premium entitlement; do not extend generic cosmetics entitlement/pending owners. |
| Daily Run chest to season ownership | `BACKEND-SEASON-DAILY-RUN-PROVENANCE-001` | Bind the persisted chest day to exactly one season window. |
| Season milestone global XP accounting | `BACKEND-SEASON-XP-SETTLEMENT-001` | Route through the canonical transaction-aware XP service; coordinate file ownership with the Daily Run season prompt. |
| Adaptive answer duplicate/conflict/cancellation | `BE-PERF-012` | Refine/execute existing owner; never create a second adaptive-answer implementation prompt. |
| Practice answer/completion exactly-once | `BE-PERF-015` | Use existing practice owner. |
| Multi-replica auth/rate-limit semantics | `BE-PERF-011` | Link residual from `BACKEND-API-DB-017`; no second limiter store. |
| Mobile single-device logout revocation / refresh expected-failure contract | `BACKEND-AUTH-REFRESH-LOGOUT-CONTRACT-001` | Normalize public contract only; do not absorb token-at-rest, rotation-race or security-stamp owners. |
| Pre-production DB/Fly idle cost budget | `BACKEND-OPS-FLY-AUTOSTOP-001` + `BACKEND-OPS-DB-IDLE-BUDGET-001` | Cost/idle policy only; preserve outage-isolation, schema-validation and business-settlement owners. |
| Adaptive session start timeout/restart | `BACKEND-XREPO-ADAPTIVE-START-001` | Existing uncovered backend owner. |

## Active-row shape

```markdown
| ID | Priority | Status | Prompt | Purpose |
|---|---:|---|---|---|
| `BACKEND-EXAMPLE-001` | P1 correctness | Ready | [Open](example/BACKEND-EXAMPLE-001.md) | One bounded observable result. |
```

Done tail stays compact:

```text
Done <n>% — Run log: <path>; Validation: <result>; Residual risk: <sentence>; Commit: self|<sha>
```

## Validation

```powershell
python scripts/validate_agent_prompt.py --changed-from <base-sha>
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
python scripts/check_documentation_health.py --full-links
```

Use full historical audits only for intentional legacy cleanup.

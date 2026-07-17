# Backend Prompt Queue Router

Last aligned: 2026-07-17  
Owner: `backend-agent-system`

Current code and executed tests override old queue prose. Completed archives and main-verified run evidence override stale Ready rows.

## Start rule

1. If the user assigned a bounded task, do it directly with `Queue: user-assigned`; no queue search/admission.
2. If the user asked for next work or named a queue, inspect this router and one highest-priority non-blocked row.
3. Refresh visible branch/PR ownership for that row only.
4. Create/promote a formal active prompt only through v2/v3 admission.
5. Do not reopen archived IDs; use one new residual ID.

## Current canonical priority

| Order | Queue/owner | Remaining ownership |
|---:|---|---|
| 1 | `backend_cross_repo_current_main_2026_07_17.md` | Adaptive session-start idempotency, durable private bug screenshots and stale PR #3 disposition. |
| 2 | `backend_performance_followups_2026_07_03.md` | Existing P0 adaptive answer/practice settlement owners `BE-PERF-012` and `BE-PERF-015`, plus bounded limiter owner `BE-PERF-011`. |
| 3 | `backend_test_coverage.md` | Provider/cancellation/regression proof that supports canonical runtime owners without reimplementing them. |
| 4 | `backend_api_db_residuals_pass3_2026_07_16.md` | Historical pass-3 evidence; `016` is superseded by `020`, `017..019` are delivered/nonclaimable. |
| 5 | `backend_failing_test_followups_2026_07_11.md` | Historical migration repair; no active row after `BACKEND-MIGRATION-001` delivery. |

`backend_critical_risk_prevention.md` and earlier pass queues are historical evidence. Their completed rows are not active selection sources.

## Existing-owner cross-repo routing

| Flutter need | Backend owner | Action |
|---|---|---|
| Adaptive answer duplicate/conflict/cancellation | `BE-PERF-012` | Refine/execute existing owner; never create a second adaptive-answer implementation prompt. |
| Practice answer/completion exactly-once | `BE-PERF-015` | Use existing practice owner. |
| Multi-replica auth/rate-limit semantics | `BE-PERF-011` | Link residual from `BACKEND-API-DB-017`; no second limiter store. |
| Adaptive session start timeout/restart | `BACKEND-XREPO-ADAPTIVE-START-001` | New uncovered backend owner. |

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

# Backend Prompt Queue Router

Last aligned: 2026-07-17  
Owner: `backend-agent-system`

## Start rule

1. If the user assigned a bounded task, do it directly with `Queue: user-assigned`; no queue search/admission.
2. If the user asked for “next work” or named a queue, inspect this router and one highest-priority non-blocked row.
3. Refresh visible branch/PR ownership for that row only.
4. Create/promote a formal active prompt only through v2/v3 admission.

## Canonical priority order

| Order | Queue | Primary ownership |
|---:|---|---|
| 1 | [`backend_critical_risk_prevention.md`](backend_critical_risk_prevention.md) | Security/auth/settlement/idempotency/release integrity |
| 2 | [`backend_api_db_residuals_pass3_2026_07_16.md`](backend_api_db_residuals_pass3_2026_07_16.md) | Current API/DB security/readiness |
| 3 | [`backend_failing_test_followups_2026_07_11.md`](backend_failing_test_followups_2026_07_11.md) | Proven migration/test blockers |
| 4 | [`backend_latest_commit_followups_2026_07_11.md`](backend_latest_commit_followups_2026_07_11.md) | Recent delivery/CI/evidence closure |
| 5 | [`backend_test_coverage.md`](backend_test_coverage.md) | Canonical test/provider packages |
| 6 | [`backend_performance_followups_2026_07_03.md`](backend_performance_followups_2026_07_03.md) | Bounded performance/operational work |
| 7 | [`backend_second_pass_risk_prevention.md`](backend_second_pass_risk_prevention.md) | Secondary auth/proxy/job/authoring risks |

One runtime/schema/ledger owner wins. Supporting test/performance/provider prompts link to it rather than reimplementing it.

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

## Current validation

```powershell
python scripts/validate_agent_prompt.py --changed-from <base-sha>
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
```

Use full evidence audit only for intentional legacy cleanup.

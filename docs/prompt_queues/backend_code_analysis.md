# Backend Code Analysis Queue

Owner: `backend-quality`
Cadence: every two weeks, plus release-candidate/manual dispatch
Canonical process: [`BACKEND_CODE_ANALYSIS_PLAYBOOK.md`](../BACKEND_CODE_ANALYSIS_PLAYBOOK.md)

| ID | Priority | Status | Prompt | Purpose |
|---|---:|---|---|---|
| `BACKEND-ANALYSIS-001` | P1 | Done 95% — Run log: `.ai/runs/2026-09-19-BACKEND-ANALYSIS-001-evidence.md`; Validation: analyzer build 0, focused auth/ownership 28/28, advisory inventory recorded, format baseline classified; Residual risk: dependency advisories and whitespace drift deferred to separate owner; Commit: self | [Periodic backend code analysis and prompt triage](BACKEND-ANALYSIS-001-periodic-code-analysis.md) | Run one bounded focus rotation, classify analyzer/risk findings and route only unique repair prompts. |

Do not treat this queue as runtime-fix evidence. Each routed prompt needs its own owner, regression proof and run log.

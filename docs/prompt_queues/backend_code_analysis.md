# Backend Code Analysis Queue

Owner: `backend-quality`
Cadence: every two weeks, plus release-candidate/manual dispatch
Canonical process: [`BACKEND_CODE_ANALYSIS_PLAYBOOK.md`](../BACKEND_CODE_ANALYSIS_PLAYBOOK.md)

| ID | Priority | Status | Prompt | Purpose |
|---|---:|---|---|---|
| `BACKEND-ANALYSIS-001` | P1 | Needs validation 79% — Run log: `.ai/runs/2026-11-10-BACKEND-ANALYSIS-001-evidence.md`; Validation: restore + Release analyzer build (0 warnings/errors) + migration-drift tests (18/18) + vulnerable-package inventory (none) passed; format verification timed out with existing WHITESPACE diagnostics; no local PostgreSQL/Neon proof; unique static risk routed to `BACKEND-MIGRATION-002`; Commit: self | [Periodic backend code analysis and prompt triage](BACKEND-ANALYSIS-001-periodic-code-analysis.md) | Run one bounded focus rotation, classify analyzer/risk findings and route only unique repair prompts. |

Do not treat this queue as runtime-fix evidence. Each routed prompt needs its own owner, regression proof and run log.

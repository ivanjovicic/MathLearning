# Backend Migration Shape Guard Queue

Owner: `backend-agent-system`
Source: `DatabaseSchemaVersionGuard` and PostgreSQL migration-drift regression tests

| ID | Priority | Status | Prompt | Purpose |
|---|---:|---|---|---|
| `BACKEND-MIGRATION-002` | P2 | Needs validation 79% — Run log: `.ai/runs/2026-11-10-BACKEND-MIGRATION-002-evidence.md`; Validation: strict Release build (0 warnings/errors), PostgreSQL migration-drift 22/22, schema-from-zero 7/7, EF model aligned; PR #53; Residual risk: 30-minute run timebox exceeded; PostgreSQL 16 CI pending; Commit: self | [Validate critical audit column shapes at startup](BACKEND-MIGRATION-002-critical-column-shape-guard.md) | Reject same-name type, length, or nullability drift in the seven critical `UserAnswerAudits` columns after migration history is current. |

Admission evidence: the startup guard currently checks these columns by name; the repair migration validates shape only when that migration is applied. No production drift is asserted. This task has no dependency on the open production quiz-answer incident or draft PR #50.

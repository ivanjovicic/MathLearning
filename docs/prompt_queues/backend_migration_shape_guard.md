# Backend Migration Shape Guard Queue

Owner: `backend-agent-system`
Source: `DatabaseSchemaVersionGuard` and PostgreSQL migration-drift regression tests

| ID | Priority | Status | Prompt | Purpose |
|---|---:|---|---|---|
| `BACKEND-MIGRATION-002` | P2 | Ready | [Validate critical audit column shapes at startup](BACKEND-MIGRATION-002-critical-column-shape-guard.md) | Reject same-name type, length, or nullability drift in the seven critical `UserAnswerAudits` columns after migration history is current. |

Admission evidence: the startup guard currently checks these columns by name; the repair migration validates shape only when that migration is applied. No production drift is asserted. This task has no dependency on the open production quiz-answer incident or draft PR #50.

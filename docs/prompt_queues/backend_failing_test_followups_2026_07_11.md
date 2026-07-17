# Backend Failing-Test Follow-up Queue — 2026-07-11

Status synchronized against backend `main` on 2026-07-17. This queue has no active claimable row.

## Completed migration blocker

| ID | Status | Result |
|---|---|---|
| `BACKEND-MIGRATION-001` | Done / archived | Historical cosmetics constraint-name drift was repaired through schema introspection; clean and upgraded PostgreSQL paths passed. |

Evidence: `.ai/runs/2026-07-13-BACKEND-MIGRATION-001-evidence.md`  
Delivered commit: `9b01a629e7571375986d85dce8075652fc680ad8`

Verified evidence records:

- generated idempotent migration SQL;
- clean PostgreSQL schema validation;
- upgrade fixture migration to latest;
- schema regression tests;
- preserved constraint/delete semantics.

The original detailed prompt remains available through Git history and the run log. Do not reopen this ID because an old workflow log referenced the pre-fix failure. Any new migration failure requires a new ID with the exact current failing SHA/run and schema evidence.

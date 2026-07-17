# Backend Completed / Superseded Archive — 2026-07-17

This archive prevents stale queue text from reopening delivered work. Current code/tests and exact run evidence remain authoritative.

| ID | Final disposition | Evidence |
|---|---|---|
| `BACKEND-CRIT-001..005`, `007`, `008` | Delivered/validated; historical critical queue only. | Owning rows and `.ai/runs/2026-06-24-*` / `2026-07-01-*` logs. |
| `BACKEND-CRIT-006` | Docs/spec completed at 85%; legacy no-key compatibility residual is owned by specific mutation prompts, not this archived ID. | `.ai/runs/2026-07-01-BACKEND-CRIT-006-evidence.md` |
| `BACKEND-MIGRATION-001` | Delivered and provider-validated; clean and upgraded PostgreSQL paths passed. | `.ai/runs/2026-07-13-BACKEND-MIGRATION-001-evidence.md`, commit `9b01a629e7571375986d85dce8075652fc680ad8` |
| `BACKEND-API-DB-017` | Auth lockout/password/enumeration policy delivered; remaining multi-replica limiter semantics routed to `BE-PERF-011`. | `.ai/runs/2026-07-16-BACKEND-API-DB-017-evidence.md`, commit `cf8ea27b03153f5c8ef6c140f7e0ebe5bf498bf4` |
| `BACKEND-API-DB-018` | Access/refresh token invalidation delivered and focused tests passed. | `.ai/runs/2026-07-16-BACKEND-API-DB-018-evidence.md`, commit `814fbf586e042a36a3a76286158a8af2d7ba4ff8` |
| `BACKEND-API-DB-019` | Versioned cosmetics catalog import/readiness ownership delivered. | `.ai/runs/2026-07-16-BACKEND-API-DB-019-evidence.md`, commit `b515950` |
| `BACKEND-API-DB-016` | Partial privacy fix delivered at 70%; original broad ID is superseded by narrow residual `BACKEND-API-DB-020`. | `.ai/runs/2026-07-16-BACKEND-API-DB-016-evidence.md`, commit `47dad8a` |

Archive status is not runtime proof by itself; it routes agents to the exact evidence and prevents duplicate claims.

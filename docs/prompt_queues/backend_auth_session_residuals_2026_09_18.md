# Backend Auth Session Residuals — 2026-09-18

Last aligned: 2026-09-18  
Status: canonical auth/session contract residual queue  
Target repo: `ivanjovicic/MathLearning`

Purpose: own current source-confirmed auth HTTP contract gaps without reopening delivered security-stamp work or duplicating token-at-rest/limiter/rotation owners.

## Active prompts

| ID | Priority | Status | Prompt | Purpose |
|---|---:|---|---|---|
| `BACKEND-AUTH-REFRESH-LOGOUT-CONTRACT-001` | P1 auth/session security | Ready | [Open](backend_auth_session_residuals_2026_09_18/BACKEND-AUTH-REFRESH-LOGOUT-CONTRACT-001.md) | Normalize refresh failure codes/correlation and make logout revocation idempotent/non-disclosing for the mobile best-effort logout flow. |

## Existing-owner boundaries

- `BACKEND-API-DB-007`: refresh-token secret-at-rest representation and retention.
- `BACKEND-TEST-015`: provider/concurrent refresh-rotation proof.
- `BACKEND-API-DB-018`: delivered security-stamp-backed JWT/refresh invalidation.
- `BE-PERF-011`: multi-replica auth/rate-limit semantics.
- `BACKEND-OPS-HEALTH-LIVENESS-001` / `BACKEND-OPS-HANGFIRE-NEON-001`: Fly/Neon host starvation and worker isolation.
- `BACKEND-API-DB-013`: registration provisioning/atomicity owner; do not reopen.

## Collision rules

1. This queue owns response/idempotency semantics only, not token storage or rotation architecture.
2. Coordinate response fixtures with mobile `MOB66-AUTH-LOGOUT-REVOCATION-001`.
3. Never expose token existence, raw bearer secrets, PII or provider exceptions.
4. Done requires focused executable tests, Release build, contract docs synchronization, main delivery and run evidence.

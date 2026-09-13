# Backend Registration Ops Residuals — 2026-09-09

Last aligned: 2026-09-09
Status: canonical residual queue for Neon/Fly host starvation found during registration incident work
Target repo: `ivanjovicic/MathLearning`
Audited baseline: `980f9c4`
Default lane: Cursor/Codex implementation lane
Purpose: keep ASP.NET liveness and Fly HTTP health responding when Neon/Hangfire/outbox workers starve thread-pool or DB connections; do not reopen mobile registration typed-error work already delivered in the registration observability fix.

Normal command:

```powershell
python scripts/prompt_agent.py next --agent <UNIQUE-AGENT-ID> --preferred-queue docs/prompt_queues/backend_registration_ops_residuals_2026_09_09.md
```

## Admission evidence

- Historical production incident: Hangfire, OutboxProcessor, Sync redrive and EF queries timed out with `NpgsqlException: The operation has timed out`; even `GET /api/health/` stopped responding until Fly machine restart.
- 2026-09-09 live probe from registration debugging: `https://mathlearning-api.fly.dev/api/health/`, `/api/health/db`, `/api/health/ready` and `OPTIONS /auth/mobile/register` all timed out within 15–20s (host unreachable, not HTTP validation).
- Current `HealthEndpoints` liveness (`GET /api/health/`) does not query DB, yet production still failed to answer — points to process/thread-pool starvation rather than missing liveness route.
- `/api/health/db` and `/ready` catch exceptions without logging exception type/correlation; DB checks are unbounded relative to Neon timeouts.
- Registration mobile endpoint now returns typed `registration_unavailable` for `DbException`, but that does not keep the host alive under worker starvation.

## Active prompts

| ID | Priority | Status | Prompt | Purpose |
|---|---:|---|---|---|
| `BACKEND-OPS-HEALTH-LIVENESS-001` | P0 reliability | Ready | [Open](backend_registration_ops_residuals_2026_09_09/BACKEND-OPS-HEALTH-LIVENESS-001.md) | Keep anonymous liveness answering under Neon/DB worker starvation with bounded DB health and Fly HTTP checks. |
| `BACKEND-OPS-HANGFIRE-NEON-001` | P1 reliability | Ready after `BACKEND-OPS-HEALTH-LIVENESS-001` | [Open](backend_registration_ops_residuals_2026_09_09/BACKEND-OPS-HANGFIRE-NEON-001.md) | Isolate Hangfire/outbox/background DB work so Neon timeouts cannot exhaust the request thread pool. |

## Ordering and collision rules

1. `BACKEND-OPS-HEALTH-LIVENESS-001` outranks Hangfire tuning because operators need a live probe first.
2. Do not reopen mobile registration password/error-contract work; that owner is the completed registration observability fix.
3. Do not invent a second CORS system; Production Flutter Web origins remain explicit `Cors:AllowedOrigins` configuration.
4. Never force-push, delete or reuse another agent's claim branch.
5. Done requires executable red-green proof, Release build, main delivery and synchronized evidence.

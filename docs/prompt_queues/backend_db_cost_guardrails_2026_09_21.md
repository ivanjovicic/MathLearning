# Backend DB Cost & Idle Guardrails — 2026-09-21

Last aligned: 2026-09-21  
Status: canonical active cost/reliability queue  
Target repo: `ivanjovicic/MathLearning`

Purpose: prevent the pre-production API from consuming a serverless PostgreSQL quota while no users are active, without weakening future full-production worker correctness.

## Active prompts

| ID | Priority | Status | Prompt | Purpose |
|---|---:|---|---|---|
| `BACKEND-OPS-FLY-AUTOSTOP-001` | P0 cost/reliability | Done 100% — Run log: `.ai/runs/2026-09-21-BACKEND-OPS-FLY-AUTOSTOP-001-evidence.md`; Validation: TOML assertions + diff check + documentation health; Residual risk: Fly deploy/idle observation remains operator follow-up; Commit: self | [Open](backend_db_cost_guardrails_2026_09_21/BACKEND-OPS-FLY-AUTOSTOP-001.md) | Stop the idle Fly machine and auto-start it on demand before real production traffic. |
| `BACKEND-OPS-DB-IDLE-BUDGET-001` | P0 cost/reliability | Ready | [Open](backend_db_cost_guardrails_2026_09_21/BACKEND-OPS-DB-IDLE-BUDGET-001.md) | Add an explicit PreProductionIdle background-work profile and remove continuous DB wake-up polling. |

## Current evidence

- provider rejected PostgreSQL connection with SQLSTATE 53000 quota exceeded on 2026-09-21;
- Outbox empty polling is 1 second;
- Hangfire PostgreSQL queue polling is 15 seconds;
- sync dead-letter polling is 60 seconds and enabled by default;
- several hourly/daily DB-backed hosted services are registered;
- Fly service is configured for auto-stop/auto-start with zero warm machines; live idle observation remains pending.

## Ordering

1. `BACKEND-OPS-FLY-AUTOSTOP-001` is delivered; live stop/start observation remains operator follow-up.
2. `BACKEND-OPS-DB-IDLE-BUDGET-001` makes the running VM itself DB-idle-friendly.
3. Preserve `BACKEND-OPS-HANGFIRE-NEON-001` outage/backoff behavior; do not regress its tests.

## Guardrails

- never commit DB credentials;
- do not disable schema validation;
- do not delete outbox/Hangfire code;
- Full profile must preserve future production semantics;
- PreProductionIdle is explicit and reversible;
- no cost-saving claim without executable tests and post-deploy observation.

# BACKEND-OPS-FLY-AUTOSTOP-001 — Auto-stop the idle Fly API machine before real production traffic

Prompt contract: v2  
Repository: `ivanjovicic/MathLearning`  
Prompt ID: `BACKEND-OPS-FLY-AUTOSTOP-001`  
Queue: `docs/prompt_queues/backend_db_cost_guardrails_2026_09_21.md`  
Priority: P0 cost/reliability  
Status: Ready  
Run mode: infrastructure config  
Delivery target: main

## Current-main evidence

- `fly.toml` exposes the API through `[[services]]` but does not set `auto_stop_machines`, `auto_start_machines` or `min_machines_running`.
- Current Fly configuration defaults auto-stop to off when it is not configured.
- The app currently has one Fly machine and one attached local volume for DataProtection keys.
- The Fly health check uses `/api/health/`, which is intentionally DB-free.
- The project is not yet serving real production traffic, so cold-start latency is preferable to keeping Fly + Postgres continuously active.

## Required work

1. Verify the current Fly configuration reference before editing.
2. Configure the existing service for:
   - `auto_stop_machines = "stop"`;
   - `auto_start_machines = true`;
   - `min_machines_running = 0`.
3. Preserve the existing internal port, HTTP/TLS ports, volume mount, DataProtection path and DB-free health check.
4. Do not add a second machine/process group or remove the volume.
5. Document expected pre-production behavior:
   - first request after idle can cold-start the API;
   - Fly automatically starts the machine;
   - idle machine can stop;
   - background hosted services stop with the machine and therefore cannot keep waking Postgres while the VM is stopped.
6. Add/extend a static Fly config validation if the repo already has one; otherwise use `fly config validate` when available and record inability honestly.
7. Do not modify runtime worker correctness in this prompt.

Owned paths:
- `fly.toml`
- deployment/operations documentation
- static config validation test only if an existing owner exists

Avoid paths:
- application DB worker implementation
- Neon secrets/connection strings
- machine count/volume deletion
- health endpoint semantics

## Required proof

- TOML/config validation;
- existing health endpoint tests unchanged;
- Release build if runtime source is touched (it should not be);
- operator follow-up command documented for deploy/status verification.

## Deployment smoke after merge

```powershell
fly config validate -a mathlearning-api
fly deploy -a mathlearning-api
fly status -a mathlearning-api
curl.exe --max-time 20 https://mathlearning-api.fly.dev/api/health/
```

Do not claim deployed savings until the machine is observed stopping after idle and auto-starting on the next request.

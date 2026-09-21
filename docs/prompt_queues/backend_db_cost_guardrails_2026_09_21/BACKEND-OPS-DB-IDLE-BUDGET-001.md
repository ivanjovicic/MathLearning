# BACKEND-OPS-DB-IDLE-BUDGET-001 — Pre-production DB wake-up budget for Outbox, Hangfire, Sync and scheduled workers

Prompt contract: v2  
Repository: `ivanjovicic/MathLearning`  
Prompt ID: `BACKEND-OPS-DB-IDLE-BUDGET-001`  
Queue: `docs/prompt_queues/backend_db_cost_guardrails_2026_09_21.md`  
Priority: P0 cost/reliability  
Status: Ready  
Run mode: known-fix + configuration hardening  
Delivery target: main

## Confirmed current-main cost drivers

Current source contains periodic DB activity that can keep a serverless Postgres compute awake even when no user is using the app:

- `OutboxProcessingOptions.IdleDelay = 1s`; `OutboxProcessor` queries every loop even when no messages were processed.
- Hangfire PostgreSQL storage uses `QueuePollInterval = 15s`.
- `Sync.EnableDeadLetterRedriveWorker = true` with `DeadLetterRedriveIntervalSeconds = 60`.
- Sync retention cleanup is enabled.
- Weakness daily analysis performs a DB read at hosted-service startup and then every 24h.
- Explanation-cache cleanup performs a DB-backed sweep every hour.
- XP reset, index maintenance and other scheduled workers can create additional periodic database activity.
- On 2026-09-21 the production connection was rejected with PostgreSQL SQLSTATE `53000` because the provider quota was exhausted.

Neon-style scale-to-zero requires a genuine idle window. Repeated queries below that window defeat the cost model.

## Existing owner boundary

`BACKEND-OPS-HANGFIRE-NEON-001` owns outage/starvation isolation and has no unique commits ahead of current main on its old agent branch as of 2026-09-21. This prompt must preserve its bounded timeout/backoff safeguards and owns **idle-cost policy**, not outage correctness.

## Required architecture

Introduce one explicit configuration authority for background DB activity, for example `BackgroundWork:Profile` with at least:

- `Full` — future real-production semantics; preserve current feature behavior.
- `PreProductionIdle` — current Fly environment while traffic is low; optimize for DB scale-to-zero.

Do not silently infer this from `ASPNETCORE_ENVIRONMENT=Production`; Fly is currently Production for configuration purposes even though the product is pre-production.

## PreProductionIdle behavior

Implement and test the smallest safe policy that achieves all of these:

1. **Hangfire**
   - do not start the PostgreSQL Hangfire server/poller when no enabled feature requires queued jobs;
   - preserve a deterministic disabled/fallback `IBackgroundJobClient`;
   - configuration must fail clearly or select an explicit safe fallback if an enabled feature requires Hangfire while the profile disables it.

2. **Sync workers**
   - dead-letter redrive and retention cleanup are disabled by default in `PreProductionIdle`;
   - manual/admin execution paths remain available where they already exist;
   - `Full` retains current behavior.

3. **Daily/hourly maintenance workers**
   - weakness daily sweep, explanation-cache cleanup, index maintenance and XP reset must be individually suppressible through the profile/derived options;
   - `Full` retains current correctness semantics;
   - do not delete worker implementations.

4. **Outbox**
   - do not leave a 1-second empty poll forever in `PreProductionIdle`;
   - preserve low latency after actual work, but when repeated polls are empty use bounded adaptive idle backoff long enough to permit a serverless DB idle/suspend window (target maximum idle delay >= 10 minutes in PreProductionIdle);
   - reset the backoff immediately after processing work;
   - Full profile may retain the existing low-latency cadence.
   - do not drop or mark pending outbox messages successful merely to reduce polling.

5. **Startup**
   - avoid unconditional DB scans solely to initialize disabled pre-production workers.
   - startup validation/migration authority remains `Database:StartupMode=ValidateExact`.

6. **Observability**
   - log one secret-safe startup summary of effective background-work profile and enabled worker names/intervals;
   - do not log connection strings or one line per empty poll;
   - add a test-visible registration summary/helper so CI can prove which DB pollers are enabled.

7. **Fly config**
   - set the current Fly deployment to `BackgroundWork__Profile=PreProductionIdle`.
   - preserve a documented one-line switch back to `Full` before real production traffic.

## Required tests

Prove at minimum:

- PreProductionIdle does not register/start Hangfire server when it is not required;
- PreProductionIdle disables sync redrive/retention periodic workers;
- PreProductionIdle disables the selected daily/hourly maintenance workers;
- Full profile still registers current workers;
- empty Outbox polling increases delay to the configured max and work resets it;
- cancellation/shutdown interrupts any long idle delay promptly;
- pending outbox work is still processed correctly;
- no secret appears in profile logs;
- current outage-isolation tests remain green.

## Owned paths

- background worker registration/config under `src/MathLearning.Api`
- `OutboxProcessor` / `OutboxProcessingOptions`
- worker options/config needed for explicit enablement
- `appsettings.json` / `fly.toml` pre-production profile
- focused tests
- backend ops docs

Avoid paths:
- auth/registration response contracts
- adaptive/economy exactly-once redesign
- DB schema/migrations
- connection-string secrets
- disabling migrations/schema validation
- deleting Hangfire/outbox functionality

## Acceptance

With `PreProductionIdle` selected and no user traffic, the application has no sub-five-minute periodic Postgres query loop except any explicitly justified owner. Full mode preserves production behavior. The effective worker set is covered by executable tests and clearly observable at startup.

## Validation

```powershell
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~Outbox|FullyQualifiedName~Hangfire|FullyQualifiedName~Background"
dotnet build src/MathLearning.Api/MathLearning.Api.csproj -c Release
git diff --check
```

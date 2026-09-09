# BACKEND-OPS-HANGFIRE-NEON-001 — Isolate Hangfire/outbox DB work during Neon outages

Prompt contract: v2
Prompt admission: v3
Repository: ivanjovicic/MathLearning
Prompt ID: BACKEND-OPS-HANGFIRE-NEON-001
Queue: docs/prompt_queues/backend_registration_ops_residuals_2026_09_09.md
Run lane: known-fix
Token budget: medium
Timebox: 30 minutes

Problem evidence:
- Production Neon timeouts previously hit Hangfire, OutboxProcessor, Sync redrive and EF queries together; API recovery required Fly machine restart.
- 2026-09-09 live health probes timed out entirely, consistent with host starvation beyond a single failing SQL call.
- Registration debugging must not confuse this infrastructure failure with HTTP 400 validation; nevertheless registration needs a live host to return typed failures.
- `BACKEND-OPS-HEALTH-LIVENESS-001` keeps probes honest but does not own background-worker concurrency/backoff.

Research packet:
- Reviewed Hangfire/outbox/background services as the likely shared starvation source under Neon latency.
- Confirmed no Ready prompt currently owns Neon-outage isolation for Hangfire/outbox without overlapping adaptive settlement owners.
- Adaptive `BE-PERF-012`/`BE-PERF-015` own mutation exactly-once semantics, not Hangfire host isolation.

Deduplication check:
- Depends on / runs after `BACKEND-OPS-HEALTH-LIVENESS-001`.
- Do not reopen adaptive answer/practice settlement owners.
- Do not change mobile registration response contract.

Priority rationale: P1 reliability because background DB workers can exhaust connections/threads and make even fixed liveness probes fail under load.

Dependencies/collisions:
- Ready after `BACKEND-OPS-HEALTH-LIVENESS-001`.
- Coordinate if touching shared DI/host lifetime with health timeout work.
- Avoid broad Hangfire feature rewrites unrelated to outage isolation.

Owner boundary:
- Owns Hangfire/outbox/background DB concurrency, timeout and backoff isolation during Neon outages.
- Does not own Fly HTTP probe wiring (previous prompt) or Flutter clients.

Queue placement: second active row in `backend_registration_ops_residuals_2026_09_09.md`.

Task: Prevent Hangfire/outbox/background Neon timeouts from exhausting the ASP.NET request thread pool or DB connection pool.

Source of truth:
- Hangfire registration and recurring jobs in `src/MathLearning.Api`
- OutboxProcessor / sync redrive hosted services
- Npgsql pool/timeout configuration
- evidence from the 2026-09-09 host timeout probe

Interpretation before work: Build the matrix `worker timeout -> concurrency/non-overlap -> backoff -> pool budget -> request-path isolation` before editing.

Ambiguity rule: Choose the smallest reversible concurrency/backoff/timeout isolation that preserves job correctness. Missing operator authority for production knobs yields one named handoff.

Risk/ownership model:
- Background work uses bounded DB commands and non-overlapping execution where already supported.
- Failures log safe reason codes and exception types without secrets.
- Request-path auth/registration behavior remains unchanged.
- No automatic infinite retry storms against Neon.

Test-first contract:
- Pre-change proof: the focused Hangfire/outbox isolation test must fail before bounded worker isolation is implemented.
- Post-change proof: the same `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~Hangfire` command must pass after implementation.
- Counterexample: unbounded retry or disabling jobs without backoff fails the change.

Failure-mode matrix:
- Neon timeout inside Hangfire job.
- OutboxProcessor holding connections across retries.
- Concurrent recurring jobs amplifying pool exhaustion.
- Logging connection strings or SQL with secrets.

Execution packet:
- Initial reads: Hangfire startup, OutboxProcessor, sync redrive, pool settings, health residual evidence; maximum 8 sources.
- Search budget: maximum 4 searches for DisableConcurrentExecution, outbox backoff and Npgsql timeout owners.
- First hypothesis/falsifier: background workers lack bounded timeout/backoff isolation; falsify by proving existing isolation already keeps request threads free under injected Neon hang.
- Expected changed files: Hangfire/outbox/hosted-service isolation, config, focused tests, evidence; maximum 6 paths.
- Focused proof: worker isolation/backoff tests under canceled or slow DB.
- Stop trigger: requires platform infra outside repository or adaptive settlement redesign.

Owned paths:
- Hangfire/outbox/background worker isolation code under `src/MathLearning.Api`
- related configuration
- focused tests under `tests/MathLearning.Tests/`
- this prompt/queue evidence

Avoid paths:
- mobile Flutter repository
- registration DTO/public error contract edits
- adaptive answer/practice exactly-once redesign (`BE-PERF-012`/`BE-PERF-015`)
- production secret values in evidence

Documentation impact: update backend ops/Hangfire durable docs when isolation contracts change; otherwise record none with reason.

Acceptance criteria:
1. Background DB work has explicit bounded timeout and backoff/concurrency isolation under Neon failure.
2. Focused tests prove request/health path remains usable when a background dependency hangs/fails after the health prompt.
3. Logs remain secret-safe.
4. No registration/auth public contract drift.
5. Release build and focused tests pass.

Proof required:
- Focused worker isolation tests.
- Evidence run log with commands and counts.
- Verified main delivery.

Validation:
```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~Hangfire
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet build src/MathLearning.Api/MathLearning.Api.csproj -c Release
```

Completion gate: Done only after verified main delivery, honest proof and synchronized evidence.

Stop conditions:
- Stop if health liveness is still unfixed and hand back to `BACKEND-OPS-HEALTH-LIVENESS-001`.
- Stop before adaptive settlement redesign.
- Stop at six changed paths or the timebox.

Evidence: .ai/runs/<yyyy-mm-dd>-BACKEND-OPS-HANGFIRE-NEON-001-evidence.md

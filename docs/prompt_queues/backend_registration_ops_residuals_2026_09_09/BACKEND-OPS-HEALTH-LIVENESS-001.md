# BACKEND-OPS-HEALTH-LIVENESS-001 — Bound health probes so Neon outages cannot hide API death

Prompt contract: v2
Prompt admission: v3
Repository: ivanjovicic/MathLearning
Prompt ID: BACKEND-OPS-HEALTH-LIVENESS-001
Queue: docs/prompt_queues/backend_registration_ops_residuals_2026_09_09.md
Run lane: known-fix
Token budget: medium
Timebox: 30 minutes

Problem evidence:
- Historical Neon timeout incident left Hangfire/Outbox/EF starved and `GET /api/health/` unresponsive until Fly restart.
- 2026-09-09 probe: `https://mathlearning-api.fly.dev/api/health/`, `/api/health/db` and `/api/health/ready` timed out (15–20s) during registration debugging; host failed before HTTP validation could be observed.
- `HealthEndpoints.cs` liveness does not touch DB, yet production still failed to answer, proving process-level starvation rather than a missing route.
- `/api/health/db` and `/ready` catch exceptions without structured exception-type/correlation logging and without an explicit bounded DB command timeout.

Research packet:
- Compared current `HealthEndpoints` with Fly deploy health expectations and the registration incident evidence above.
- Confirmed no active Ready owner already owns Fly HTTP liveness isolation from Neon worker starvation.
- `BACKEND-TEST-026` owns public health information minimization, not liveness under DB starvation.
- Registration typed `registration_unavailable` handling is already delivered and must not be reopened.

Deduplication check:
- Not owned by season, adaptive, or screenshot prompts.
- Do not duplicate mobile registration error-contract work.
- Extend existing health endpoints/deploy config rather than inventing a second health system.

Priority rationale: P0 reliability because operators and Fly cannot recycle or route traffic when anonymous liveness itself hangs.

Dependencies/collisions:
- May run before `BACKEND-OPS-HANGFIRE-NEON-001`.
- Avoid rewriting Hangfire job bodies in this prompt; only health/probe/timeout seams.
- Do not change public auth/registration contracts.

Owner boundary:
- Owns anonymous liveness, bounded DB/ready probes, Fly HTTP health-check wiring and safe health logging.
- Does not own Hangfire concurrency redesign (next prompt) or mobile client changes.

Queue placement: first active row in `backend_registration_ops_residuals_2026_09_09.md`.

Task: Keep `GET /api/health/` answering under Neon/DB worker pressure and make `/db` plus `/ready` bounded with safe structured failure logs.

Source of truth:
- `src/MathLearning.Api/Endpoints/HealthEndpoints.cs`
- Fly/deploy health configuration used by the API
- current Npgsql/EF timeout configuration
- `.ai/runs` registration/ops evidence from 2026-09-09

Interpretation before work: Build the matrix `DB-free liveness -> bounded /db -> bounded /ready -> Fly HTTP check target -> secret-safe failure logs` before editing.

Ambiguity rule: Prefer the smallest reversible timeout/isolation change that keeps liveness free of DB awaits. Missing Fly config authority yields one named handoff, not a guessed cloud rewrite.

Risk/ownership model:
- Liveness remains DB-free and must not await EF/Hangfire.
- DB/ready checks use explicit bounded timeouts and return 503 without hanging.
- Logs include exception type, correlation/trace id and safe reason codes only; never connection strings, passwords or full exception messages with secrets.
- Registration and auth contracts stay unchanged.

Test-first contract:
- Pre-change proof: the focused health liveness-under-DB-hang test must fail before bounded probes are implemented.
- Post-change proof: the same `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~Health` command must pass after implementation.
- Counterexample: unbounded DB wait or logging of connection-string/password material fails the change.

Failure-mode matrix:
- Neon connection timeout during `/db` or `/ready`.
- Thread-pool starvation from background workers while liveness is probed.
- Fly marks the machine unhealthy because liveness hangs.
- Health failure logging leaks secrets.

Execution packet:
- Initial reads: HealthEndpoints, deploy/Fly health config, Npgsql timeout settings, queue evidence; maximum 8 sources.
- Search budget: maximum 4 searches for health timeout, Fly http_service checks and Hangfire collision.
- First hypothesis/falsifier: liveness hangs because the host is starved or DB-bound work shares the critical path; falsify by proving a DB-free liveness path answers under injected DB hang.
- Expected changed files: HealthEndpoints, deploy/health config, focused health tests, queue/evidence; maximum 6 paths.
- Focused proof: health endpoint tests with canceled/slow DB dependency.
- Stop trigger: requires Hangfire redesign or platform secret authority outside this owner.

Owned paths:
- `src/MathLearning.Api/Endpoints/HealthEndpoints.cs`
- Fly/deploy health check configuration for the API
- focused health tests under `tests/MathLearning.Tests/`
- this prompt/queue evidence

Avoid paths:
- `AuthEndpoints` mobile registration contract
- mobile Flutter repository
- Hangfire job implementation rewrite (owned by `BACKEND-OPS-HANGFIRE-NEON-001`)
- AllowAnyOrigin production CORS changes
- secret values in repo/evidence

Documentation impact: update API health/ops durable docs only if probe contracts change; otherwise record none with reason.

Acceptance criteria:
1. Anonymous liveness remains DB-free and answers while DB health is forced slow/unavailable in tests.
2. `/api/health/db` and `/ready` complete within an explicit bound and return safe 503 on DB failure.
3. DB health failures log exception type + correlation/trace + safe reason, never secrets.
4. Fly HTTP service health check targets liveness (or documented equivalent) rather than a DB-bound probe.
5. Focused tests and Release API build pass.

Proof required:
- Focused health tests covering liveness-under-DB-hang and bounded DB failure.
- Deploy/health config diff or explicit operator handoff if Fly file is unavailable locally.
- Evidence run log with commands and pass/fail counts.

Validation:
```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~Health
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet build src/MathLearning.Api/MathLearning.Api.csproj -c Release
python scripts/check_documentation_health.py --context src/MathLearning.Api/Endpoints/HealthEndpoints.cs
```

Completion gate: Done only after verified main delivery, honest proof and synchronized evidence.

Stop conditions:
- Stop if Hangfire isolation is required first and name `BACKEND-OPS-HANGFIRE-NEON-001`.
- Stop before auth/registration contract edits.
- Stop at six changed paths or the timebox.

Evidence: .ai/runs/<yyyy-mm-dd>-BACKEND-OPS-HEALTH-LIVENESS-001-evidence.md

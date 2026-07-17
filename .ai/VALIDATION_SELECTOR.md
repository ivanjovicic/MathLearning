# Backend Validation Selector

Last aligned: 2026-07-17  
Owner: `backend-agent-system`

Choose the narrowest executable proof that confirms or falsifies the changed behavior. Do not start with the full solution suite unless the task owns release readiness or focused proof exposes wider risk.

## Evidence order

```text
current contract/reproducer
→ smallest changed-file/static check
→ nearest focused behavior and counterexample
→ provider/build proof required by the risk
→ changed docs/prompt/evidence checks
→ wider suite/exact CI only for a named wider risk
```

Existence, compilation, source searches and queued CI are supporting information, not behavior proof.

## Documentation and agent tooling

```powershell
python -m unittest -v scripts/test_check_documentation_health.py
python scripts/check_documentation_health.py --full-links
python scripts/validate_agent_system.py
python scripts/validate_agent_prompt.py --changed-from <base-sha>
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
python scripts/analyze_agent_runs.py --changed-from <base-sha> --fail-on-regression
```

Use `python scripts/check_documentation_health.py --context <path>` before broad doc reading. Manifest/registry drift, broken registered links and unresolved conflict markers are blocking failures. Historical prompt prose remains historical until materially changed.

## Docs-only change

Also verify:

- no runtime/test/schema/build paths changed;
- generated registry matches the manifest;
- durable index links are registered;
- completion claims remain documentation/process-only;
- skipped .NET/provider checks have a specific reason.

## Endpoint/service behavior

```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~<FocusedName>
```

Select relevant counterexamples: unauthenticated/wrong-role/cross-user, duplicate/conflict, rollback/cancellation, stale/retried operation, safe error projection and provider-sensitive behavior.

## Auth and user scope

Prove anonymous, correct-user, cross-user/wrong-role and actor/target separation. Do not infer authorization from metadata alone. Token/session changes also prove revoke/lockout/role/delete invalidation and refresh behavior.

## Idempotency, rewards and settlement

Prove first request, exact replay, changed-payload conflict, rollback/cancellation, cross-user isolation and concurrent provider race. A generic retry is forbidden until the same operation identity reconstructs the settled result.

For cross-repo retry semantics, record backend and Flutter main SHAs and the existing/new owner. Backend prompts use backend commands only.

## EF Core model or migration

Inspect mapping, migration order and snapshot. Prove clean schema and relevant upgrade path with PostgreSQL, exact constraints/index/delete actions, idempotent SQL generation and readiness/startup behavior. InMemory does not prove locks, transactions or constraints.

## Background jobs/outbox/maintenance

Prove bounded capacity, deterministic claim/lease, cancellation, retry/backoff, duplicate suppression, restart and multi-replica behavior. Prefer barriers/fake clocks/interceptors over sleeps.

## Python agent/documentation tooling

```powershell
python -m py_compile scripts/<changed-script>.py
python scripts/run_guarded.py --timeout-seconds 60 -- python -m unittest -v scripts/<focused-test>.py
```

Agent/docs system package includes documentation-health, run planning, evidence, speed, prompt, wiring and CI-classifier tests.

## Build and wider suite

```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet build MathLearning.slnx -c Release --no-restore
```

Broaden only for shared startup/persistence/test infrastructure, several approved high-risk owners, focused wider-regression evidence or explicit release validation. Runtime/test/schema/build paths trigger the full `Database Validation` workflow; docs/agent-tooling-only paths use its stable skip gate.

## CI honesty

Do not claim CI green without the exact target SHA, workflow/run, jobs, required artifacts and final conclusion. `queued`/`in_progress` is not passing evidence. When the status endpoint is unavailable, report that limitation and do not fabricate success.

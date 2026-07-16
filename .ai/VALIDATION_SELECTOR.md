# Backend Validation Selector

Last aligned: 2026-07-17  
Owner: `backend-agent-system`

Start with the smallest executable proof. Do not run the full suite first and do not repeat an unchanged failure.

## Universal order

```text
current contract/reproducer
→ nearest focused behavior and counterexample
→ provider/build/static proof required by the risk
→ changed docs/prompt/evidence checks
→ wider suite/CI only for a named wider risk
```

All blocking `dotnet`, Python test, network Git and GitHub CLI commands use `scripts/run_guarded.py` with at most 180 seconds per command.

## Docs, queues, run logs and agent tooling

```powershell
git diff --check
python scripts/validate_agent_prompt.py --changed-from <base-sha>
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
python scripts/analyze_agent_runs.py --changed-from <base-sha> --fail-on-regression
python scripts/validate_agent_system.py
```

Run only the applicable commands. Historical evidence cleanup uses the manual full audit; current work uses changed-range validation.

## Python agent/CI scripts

```powershell
python -m py_compile <changed-script>
python -m unittest -v <focused-test-module>
```

Do not compile/test every Python script unless the central workflow is the task owner.

## .NET known fix

```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "<focused filter>"
```

Add the smallest regression test that fails before the fix. Run build only when compilation/shared contracts changed or the focused test requires it.

## Endpoint/auth/contract

Prove the relevant subset:

- anonymous 401;
- authenticated wrong role/user 403 or safe not-found policy;
- correct user/role success;
- request-body `userId` cannot override authenticated identity;
- exact response shape/no sensitive leakage;
- Flutter sync/defer decision when public contract changes.

## Idempotency/economy

Prove first request, duplicate same payload, conflict policy, failure/cancellation rollback, cross-user isolation and provider-backed concurrency where required. Do not add generic retry before exact replay semantics are proven.

## EF Core/migration/PostgreSQL

Use a separate migration phase when runtime+schema+operator changes exceed one owner. Required proof may include:

```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet ef migrations has-pending-model-changes --project <infra> --startup-project <api> --context ApiDbContext
python scripts/run_guarded.py --timeout-seconds 180 -- pwsh scripts/db/validate-schema.ps1 <safe args>
```

Never weaken schema-from-zero or substitute InMemory for provider-sensitive behavior.

## Full database workflow

`Database Validation` first classifies changed paths. It runs the expensive PostgreSQL/full-suite lane only for runtime, tests, migrations, solution/build metadata or DB scripts. Docs/agent-tooling-only changes receive a successful skip gate.

A green focused agent-system workflow is sufficient for a docs/agent-only change; unrelated pre-existing .NET failures are reported, not treated as target regressions.

## Stop/expand rule

Expand validation only when the focused result proves a wider risk. A timeout or failure gets one classified changed retry. If required proof still cannot run, stop in `Needs validation` or `Blocked` and create one bounded owner.

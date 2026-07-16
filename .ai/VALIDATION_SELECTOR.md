# Backend Validation Selector

Last aligned: 2026-07-16  
Owner: `backend-agent-system`

Choose the narrowest executable proof that can confirm or falsify the changed backend behavior. Do not start with the full solution suite unless the task owns release readiness or a focused failure proves wider risk.

## Evidence order

```text
current contract or reproducer
→ smallest changed-file/static check
→ nearest focused behavior and counterexample test
→ prompt/evidence/documentation checks
→ wider suite only for a named wider risk
→ exact GitHub Actions run/artifacts when CI proof is required
```

Test-file existence, compilation alone, source-string searches and queued CI are supporting evidence, not behavior proof.

## Prompt and queue changes

```powershell
python scripts/validate_agent_prompt.py docs/prompt_queues/<changed-file>.md
python scripts/validate_agent_system.py
python scripts/validate_agent_evidence.py --referenced-run-logs-only
```

Newly admitted active work must satisfy `Prompt admission: v3` in [`PROMPT_LINT_CHECKLIST.md`](PROMPT_LINT_CHECKLIST.md). Historical prompt prose remains historical until materially changed.

## Docs-only changes

```powershell
git diff --check
python scripts/validate_agent_system.py
```

Also verify linked paths exist, no runtime files changed and skipped executable checks are recorded honestly.

## Endpoint/service behavior

```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~<FocusedName>
```

Select relevant counterexamples: unauthenticated/wrong-role/cross-user access, duplicate or conflicting request, rollback/cancellation, stale/retried operation, safe error projection and provider-sensitive behavior.

## Auth and user scope

Prove anonymous behavior, correct-user behavior, cross-user/wrong-role rejection and actor/target separation for admin flows. Do not infer authorization correctness from metadata alone.

## Idempotency, rewards and settlement

Prove first request mutates once, duplicate same payload replays the settled result, conflicting duplicate follows owning policy, failure/cancellation does not leave completed ledger with unapplied domain state, and different users remain isolated. Use PostgreSQL where transaction/constraint semantics matter.

## EF Core model or migration changes

Inspect model mapping and migration order, schema creation from zero, relevant upgrade path, unique/index scope, foreign keys and rollback behavior. InMemory tests do not prove PostgreSQL constraints, locks or transactions.

## Background jobs/outbox/maintenance

Prove deterministic claim/lease behavior, cancellation, retry/backoff, duplicate suppression and concurrency. Prefer barriers, fake clocks or interceptors over sleep-based tests.

## Python agent tooling

```powershell
python -m py_compile scripts/<changed-script>.py
python scripts/run_guarded.py --timeout-seconds 60 -- python -m unittest -v scripts/<focused-test>.py
```

Agent-system package:

```powershell
python scripts/run_guarded.py --timeout-seconds 60 -- python -m unittest -v scripts/test_run_guarded.py
python scripts/run_guarded.py --timeout-seconds 60 -- python -m unittest -v scripts/test_validate_agent_prompt.py
python scripts/run_guarded.py --timeout-seconds 60 -- python -m unittest -v scripts/test_validate_agent_system.py
python scripts/validate_agent_system.py
```

## Build and wider suite

```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet build MathLearning.slnx -c Release --no-restore
```

Broaden only when focused tests reveal wider regression, shared startup/persistence/test infrastructure changed, several approved high-risk owners changed, or the task explicitly owns release validation. If proof cannot fit command policy, move it to repository CI and record the exact required workflow/artifacts.

## CI honesty

Do not claim CI green without inspecting the exact run for the target SHA. `queued` or `in_progress` is not passing evidence. Record workflow, run URL/id, target SHA, jobs, required artifacts and final conclusion.

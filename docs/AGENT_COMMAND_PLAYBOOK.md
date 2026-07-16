# Backend Agent Command Playbook

Last aligned: 2026-07-16  
Scope: repository-aware assistants working in `ivanjovicic/MathLearning`.

Purpose: keep every command short, attributable, cancellable and diagnosable. Mechanical owner: [`scripts/run_guarded.py`](../scripts/run_guarded.py).

## Hard limits

```text
one executable per command line
maximum 180 characters
maximum 3 explicit path operands
maximum 180 seconds per agent command
no &&, || or semicolon process chaining
```

Work that cannot produce useful output inside three minutes must be narrowed, split or moved to a named CI lane.

## Commands that must be guarded

Run these through `scripts/run_guarded.py`:

- `dotnet restore`, `dotnet build`, `dotnet test`, `dotnet format`;
- package restore/build/test commands that may block;
- non-trivial Python test commands;
- `git fetch`, `git pull`, `git push`;
- `gh pr checks`, `gh pr merge`, `gh run watch`;
- network, database or container commands whose duration is not trivially bounded.

Short inspection may run directly: `git status --short`, `git diff --stat`, `git diff --check`, `python -m py_compile <one-file>` and fast repository validators.

## Canonical form

```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~QuizAnswer
```

Use shorter limits where possible:

```powershell
python scripts/run_guarded.py --timeout-seconds 60 -- python -m unittest -v scripts/test_validate_agent_prompt.py
python scripts/run_guarded.py --timeout-seconds 120 -- dotnet build MathLearning.slnx -c Release --no-restore
```

The runner executes an argument vector, streams combined output, kills the process tree and returns:

```text
124 = wall-clock timeout
125 = idle-output timeout
64  = invalid guard configuration
```

## Shell and path rules

Identify the shell before execution, but keep one executable per line. A command may name at most three explicit paths, including the guard script. Prefer one production owner or one focused test owner per proof command.

Bad:

```powershell
dotnet build MathLearning.slnx && dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj
```

Good:

```powershell
python scripts/run_guarded.py --timeout-seconds 120 -- dotnet build MathLearning.slnx -c Release --no-restore
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~UserScope
```

Stop after the first failure and classify it. Do not execute later checklist commands merely to produce more output.

## Backend validation ladder

1. Reproduce/assert one contract.
2. Run the nearest focused behavior/counterexample test.
3. Build only when compilation/shared-reference risk warrants it.
4. Run PostgreSQL-backed proof when constraints, transactions or concurrency matter.
5. Run prompt/evidence/documentation validators for agent-system changes.
6. Broaden only after a named wider-risk signal.
7. Inspect exact CI run/artifacts when workflow evidence is part of completion.

Do not start with the full test suite by habit. Do not clear caches, recreate migrations or weaken assertions to obtain green output.

## Timeout response

Record:

```text
classification: product | test harness | database/provider | environment | prompt | evidence
last useful output age:
next action: narrower proof | one changed retry | CI handoff
```

One retry is allowed only after changing the cause, configuration or proof level. Repeating the same timed-out command blocks Done.

## Database/external-service safety

- State target environment and connection ownership before execution.
- Never print secrets or full connection strings.
- Prefer repository fixtures/CI services over ad-hoc production-like targets.
- Destructive commands require explicit ownership and a verified non-production target.
- InMemory results are not PostgreSQL proof.

## Documentation and prompt validation

```powershell
python scripts/validate_agent_prompt.py docs/prompt_queues/<changed-file>.md
python scripts/validate_agent_evidence.py --referenced-run-logs-only
python scripts/validate_agent_system.py
```

These checks do not replace runtime tests; runtime tests do not replace evidence/status consistency.

## Git delivery

```powershell
git status --short
git diff --stat
git diff --check
python scripts/run_guarded.py --timeout-seconds 60 -- git fetch origin main
python scripts/run_guarded.py --timeout-seconds 120 -- git push -u origin agent/backend-fix
```

Avoid without explicit approval: `git add .`, `git reset --hard`, `git clean -fd`, `git push --force`.

## Run-log command format

```text
- `<command>` (<duration>) -> pass
- `<command>` (<duration>) -> fail - <first useful cause>
- `<command>` (<duration>) -> timeout - wall | idle | environment
- `<planned command>` (unknown - connector cannot execute) -> not run - <reason>
```

A planned command is never reported as passed, and queued CI is never reported as green.

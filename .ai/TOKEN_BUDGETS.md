# Backend Agent Time and Context Budgets

Last aligned: 2026-07-17  
Owner: `backend-agent-system`

The goal is fast correct closure, not maximum reading. Evidence, queue work and CI waiting count as task time.

## Budgets

| Budget | Hard time | Workflow reads | Source/test reads | Changed files | Searches | Use for |
|---|---:|---:|---:|---:|---:|---|
| `micro` | 8 min | 2 | 4 | 2 | 1 | one known docs/tooling fix, status sync, tiny regression |
| `low` | 15 min | 3 | 8 | 3 | 2 | one known bug, focused investigation, docs/evidence |
| `medium` | 30 min | 5 | 15 | 6 | 4 | one feature slice or cross-file bug |
| `high` | 30 min/phase | 8 | 20 | 10 | 6 | finite audit or migration phase; implementation stays separate |

`Files inspected` in compact evidence includes workflow docs plus source/tests. The validator permits a small combined ceiling (`micro` 6, `low` 11, `medium` 20, `high` 28).

## Checkpoints

### Micro

- minute 2: owner/hypothesis/proof known;
- minute 5: patch and focused check started;
- minute 7: closure only;
- minute 8: stop.

### Low/medium/high phase

- minute 5: owner/source/collision confirmed;
- minute 10: root cause or finite audit findings proven;
- minute 20: smallest patch/artifact exists and focused validation started;
- minute 25: no new discovery or scope expansion;
- minute 30: stop and hand off residual work.

One command gets at most 180 seconds. One changed retry is allowed after classification; an unchanged retry is waste and ends the run.

## Automatic split rules

Split before editing when any is true:

- more than one authoritative writer/subsystem;
- implementation plus migration/bootstrap plus release review;
- more changed files than the selected budget;
- a migration changes runtime, operator workflow and broad docs in one slice;
- audit plus implementation;
- full-suite/CI repair is needed in addition to the target fix;
- owner/root cause is still uncertain at the first checkpoint.

A task may update its owning docs and one focused test without becoming a second subsystem. Twenty-file implementation packages are never a single medium run.

## Evidence overhead budget

- start packet/log creation: target under 60 seconds;
- evidence closure: target under 3 minutes;
- compact v2 log: target 35–70 lines, hard warning above 90;
- queue completion row: one compact line linking the run log;
- do not enumerate every file read when a count and focused inspection summary are sufficient;
- use `Commit SHA: self`; never create a follow-up commit only to backfill the SHA.

## Completion consequences

A read/search/change/time breach:

- blocks `Done` above 79%;
- requires an exact handoff/follow-up owner;
- is recorded as a learning signal;
- does not authorize more discovery.

Failed required validation also caps completion at 79% and requires `Needs validation`, `Blocked` or another honest non-Done state.

## Required compact metrics

```text
Run mode: one lane only
Token budget: micro | low | medium | high
Started at UTC:
Completed at UTC:
Elapsed time:
Files inspected: <n>
Files changed: <n>
Searches: <n>
Validation runs: <n>
Failed retries: <n>
```

Use `scripts/agent_run.py` to record these automatically. Use `scripts/analyze_agent_runs.py` to measure unknown elapsed time, mixed lanes, oversized logs and repeated waste.

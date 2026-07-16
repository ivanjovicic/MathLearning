# Backend Agent Time and Context Budgets

Last aligned: 2026-07-16  
Owner: `backend-agent-system`

Choose a timebox and a total-context budget before every non-trivial run. Workflow docs, source/tests, searches, diffs, logs, retries and validation output all count.

## Hard time limits

| Budget | Maximum elapsed time | Intended work |
|---|---:|---|
| low | 15 minutes | one known fix, focused investigation, docs repair or review |
| medium | 30 minutes | one feature slice or cross-file bug |
| high | 30 minutes per phase | audit/migration split into separate bounded phases |

No single implementation phase exceeds 30 minutes. High budget permits more context, not unlimited wall time.

## Checkpoints

- minute 5: prompt, owner, source and collisions confirmed;
- minute 10: root cause/falsifier or finite audit owner list proven;
- minute 20: smallest patch/artifact exists and focused validation started;
- minute 25: no new discovery or scope expansion;
- minute 30: stop, record evidence and hand off any residual work.

One command gets at most 180 seconds. A timeout is classified before one changed retry; repeated timeout stops the run.

## Budget limits

| Budget | Workflow/docs reads | Source/test files inspected | Files edited | Searches |
|---|---:|---:|---:|---:|
| low | 3 | 8 | 3 | 2 |
| medium | 5 | 15 | 6 | 4 |
| high | 8 | 20 | 10 | 6 |

A known-fix run spends at most half its budget before first edit. If ownership/root cause is still unknown, convert to investigation and hand off implementation.

## Required execution reservation

Before editing, record:

```text
Run timebox: 15 | 30 minutes
Initial reads: exact paths; maximum N
Search budget: maximum N; one question per search
Expected changed files: exact paths/prefixes; maximum N
First hypothesis/falsifier:
Focused proof and <=180-second command:
Minute-10, minute-20, minute-25 and hard-stop actions:
```

## Automatic split triggers

Split before execution when expected work includes:

- more than one independent subsystem or authoritative writer;
- audit plus implementation plus review;
- runtime change plus generated migration/setup plus broad release validation;
- auth, settlement or schema behavior combined with unrelated performance work;
- more files/searches than the selected budget;
- uncertain owner after the initial packet.

## Completion consequences

A time/context/search/edit breach:

- blocks `Done`;
- caps completion at 79%;
- requires a bounded existing or new follow-up owner;
- becomes a learning signal rather than permission to continue.

## Required run-log metrics

```text
Token budget: low | medium | high
Run timebox: 15 | 30 minutes
Elapsed time: <duration> | unknown-not-recorded
Timebox result: within | exceeded
Deadline action: completed | handed off | stopped
Files inspected: <count>
Files changed: <count>
Searches: <count>
Validation runs: <count>
Failed retries: <count>
```

## Stop rules

Stop when the selected timebox expires, a second subsystem appears, two hypotheses are falsified, the same failure repeats after one changed retry, required proof cannot run, another worker owns overlapping paths, or authority remains ambiguous.

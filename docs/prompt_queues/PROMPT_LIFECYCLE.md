# Backend Prompt Lifecycle

Last aligned: 2026-07-17  
Owner: `backend-agent-system`

## Two entry paths

### User-assigned bounded work

The user request plus the task branch/PR is the owner. Do not search all queues, create a duplicate prompt or run admission ceremony. Use `Queue: user-assigned`, record the bounded packet and proceed.

### Formal queue work

Use the router, deduplicate, admit/claim, implement, validate, deliver and archive.

```text
DISCOVER → DEDUPLICATE → ADMIT → CLAIM → PLAN → PATCH/PROVE → DELIVER → VERIFY → ARCHIVE/HANDOFF
```

## Canonical states

| State | Claimable | Meaning |
|---|---:|---|
| `Investigation` | no | Finite question before safe implementation. |
| `Blocked` | no | Named dependency/authority/proof unavailable. |
| `Ready after <ID>` | no | Admitted but prerequisite not delivered. |
| `Ready` | yes | One bounded owner, no visible collision. |
| `In progress` | no | Explicit branch/PR owns the row/paths. |
| `Needs validation` | no | Implementation exists; required proof missing/failed. |
| `Needs evidence sync` | no | Delivery exists; evidence/status incomplete. |
| `Needs merge` | no | Validated branch not in target branch. |
| `Done` | no | Proof, delivery and compact evidence synchronized. |
| `Archived` | no | Completed/superseded history. |

Code written, tests added, PR open or CI queued are not Done.

## Fast claim/collision check

For a queue task check only:

1. owning row and linked prompt;
2. current code/test owner;
3. visible branch/PR for the same ID/paths;
4. completed evidence for duplicate/residual verdict.

Do not scan every historical queue. `MISTAKE_INDEX.json` selects relevant process cards.

## Scope and split

One task has one lane, one authoritative writer and one bounded proof. Split when:

- runtime + migration/bootstrap + broad release/docs review;
- second subsystem/authoritative owner;
- more changed files/searches than budget;
- second hypothesis is falsified;
- full CI repair becomes separate from the target fix.

## Validation and delivery

Use [`.ai/VALIDATION_SELECTOR.md`](../../.ai/VALIDATION_SELECTOR.md). Changed queue/evidence checks:

```powershell
python scripts/validate_agent_prompt.py --changed-from <base-sha>
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
python scripts/analyze_agent_runs.py --changed-from <base-sha> --fail-on-regression
```

For direct main, record main SHA. For PR delivery, record PR, head SHA, targeted checks, merge SHA and main verification.

## Compact closure

Before Done:

- target and counterexample proof executed;
- required provider/CI proof executed or honest non-Done state used;
- no collision/scope breach above 79%;
- compact v2 log closed;
- queue row uses the compact tail;
- material residual work has one owner.

```text
Done <n>% — Run log: <path>; Validation: <result>; Residual risk: <sentence>; Commit: self|<sha>
```

Archive completed rows without copying full evidence into the queue.

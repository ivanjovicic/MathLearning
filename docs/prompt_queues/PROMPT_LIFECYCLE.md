# Backend Prompt Lifecycle

Last aligned: 2026-07-16  
Owner: `backend-agent-system`

This document owns queue state semantics for `ivanjovicic/MathLearning`. GitHub branch/PR ownership and explicit user assignment are the available distributed coordination evidence; do not invent a local-only claim protocol.

## State model

```text
DISCOVER → DEDUPLICATE → ADMIT → ASSIGN/CLAIM → INTERPRET → IMPLEMENT/VALIDATE → DELIVER → VERIFY → ARCHIVE/HANDOFF
```

## Canonical states

| State | Claimable | Meaning |
|---|---:|---|
| `Investigation` | no | A finite question must be answered before implementation ownership is safe. |
| `Blocked` | no | A named dependency, authority decision or unavailable proof prevents work. |
| `Ready after <ID>` | no | Scope is admitted but prerequisite delivery/evidence is not verified. |
| `Ready` | yes | Admission checks pass and no visible owner/collision blocks the slice. |
| `In progress` | no | One explicit worker/branch/PR owns the prompt and paths. |
| `Needs validation` | no | Implementation/tests exist but required executable proof is missing/failed. |
| `Needs evidence sync` | no | Runtime/delivery exists but queue/run-log/commit proof is incomplete. |
| `Needs merge` | no | Validated branch/PR is not delivered to the required target. |
| `Done` | no | Required proof, commit/delivery and evidence/status synchronization are complete. |
| `Archived` | no | Completed/superseded history; never selected as active work. |

Do not use `Done` for “code written”, “tests added”, “branch pushed”, “PR open” or “CI queued”.

## Discovery and deduplication

Before adding/promoting work:

1. inspect the canonical queue index in [`README.md`](README.md);
2. search active IDs and purposes;
3. inspect current code/tests and completed evidence for an existing owner;
4. inspect visible open PRs/branches/commits when tooling permits;
5. classify work as new, residual, extension, supersession or duplicate;
6. link supporting test/performance/provider work to one canonical runtime owner.

Record exact sources checked and the verdict.

## Assignment and collision rules

- Explicit user assignment owns the requested bounded task.
- Formal queue work uses a dedicated `agent/<description>` branch or existing non-default task branch.
- An open PR/current branch names prompt IDs and owned paths in its description/evidence.
- Two workers must not edit the same runtime owner, migration chain, queue row or evidence log concurrently.
- Provider/test prompts may run in parallel only when they do not modify the runtime owner and fixture ownership is explicit.
- Refresh current `main`, queue status and visible PR state before starting/resuming.
- Never create a duplicate prompt merely because another owner is in progress.

## Interpretation gate

Before editing, record:

```text
Interpreted outcome:
Source of truth used:
Assumptions:
Material ambiguity:
Risk/ownership model:
Failure modes:
Initial reads/search budget:
Expected changed files/limit:
Focused proof:
Stop/handoff trigger:
Documentation impact expectation:
Working branch/PR:
Delivery target:
```

Material auth, authority, persistence, API/schema, privacy/security, idempotency/settlement or destructive-migration ambiguity stops implementation.

## Scope drift

A path outside the execution packet is not silently added. Record why it belongs to the same owner. A second unexpected subsystem or second falsified hypothesis ends implementation and creates a bounded handoff.

## Validation and delivery

Use [`.ai/VALIDATION_SELECTOR.md`](../../.ai/VALIDATION_SELECTOR.md) and [`docs/AGENT_COMMAND_PLAYBOOK.md`](../AGENT_COMMAND_PLAYBOOK.md).

```text
local changes / commit / pushed branch / open PR != Done
required proof + delivered commit + exact target verification + synchronized evidence/status = Done candidate
```

For direct-to-main, record exact main SHA. For PR delivery, record PR number, head SHA, checks reviewed, merge SHA and main verification. Connector-only work records local commands as not run.

## Closure and archive

Before Done:

- target and counterexample proof executed;
- provider/CI proof executed when required;
- no unresolved scope collision;
- run log contains exact files, commands, skipped proof, mistakes, docs impact and commit SHA;
- queue row matches actual evidence;
- cross-repo impact is recorded;
- material residual work has an existing/newly admitted owner.

Move completed prompts out of active tables into dated history/archive. Preserve evidence and failed-validation history.

## Global selection rule

One empty queue is not proof that no backend work remains. Follow [`README.md`](README.md), dependencies and priority. When all routes are blocked, report exact blockers instead of creating duplicate speculative work.

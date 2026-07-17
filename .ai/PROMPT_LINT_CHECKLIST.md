# Backend Formal Prompt Contract and Admission Gate

Last aligned: 2026-07-17  
Owner: `backend-agent-system`

Use this only for a new/materially rewritten **formal active queue prompt**. A bounded user-assigned task starts directly through `scripts/agent_run.py` and does not need global queue ceremony.

## Forward-only contract

- Historical unchanged prose remains historical.
- New/materially rewritten non-trivial prompts use `Prompt contract: v2`.
- New/promoted claimable rows also use `Prompt admission: v3`.
- Status/evidence-only edits do not trigger migration.

```powershell
python scripts/validate_agent_prompt.py --changed-from <base-sha>
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
```

## Admission minimum

A prompt becomes `Ready` only when current evidence proves one finite gap, one authoritative owner is named, current queues/evidence/visible PRs were checked, dependencies/collisions are explicit, impact is concrete and the work fits one bounded lane.

Required v3 fields:

```text
Prompt admission: v3
Problem evidence:
Deduplication check:
Priority rationale:
Dependencies/collisions:
Owner boundary:
Queue placement:
```

Required v2 fields:

```text
Prompt contract: v2
Repository:
Prompt ID:
Queue:
Run lane:
Token budget:
Timebox:
Task:
Source of truth:
Interpretation before work:
Ambiguity rule:
Risk/ownership model:
Failure-mode matrix:
Execution packet:
Owned paths:
Avoid paths:
Documentation impact:
Acceptance criteria:
Proof required:
Validation:
Completion gate:
Stop conditions:
Evidence:
```

Repository is `ivanjovicic/MathLearning`. One prompt has one lane from `known-fix`, `investigation`, `validation-only`, `tests`, `docs-evidence`, `audit` or `review`.

## Bounded packet

Name exact initial reads, numeric search/edit limits, one hypothesis/falsifier, focused proof within 180 seconds and a stop trigger. Reject whole-repository scans, “fix everything”, unlimited discovery, mixed implementation/migration/audit/review and a medium task expected to touch more than six paths.

## Cross-repository admission

A backend prompt created from Flutter evidence additionally records:

- exact backend and Flutter main baselines;
- Flutter prompt/contract dependency without embedding Flutter paths/commands;
- existing backend runtime owners checked;
- why the behavior is uncovered rather than a duplicate;
- request/response/operation identity and duplicate/conflict/timeout semantics;
- synchronization or named handoff decision.

If an existing backend owner covers the mutation, update/link that owner instead of allocating a new ID. Mobile provider lifecycle and backend settlement are separate owners.

## Authority and proof

Authenticated server identity owns learner writes; existing services/ledgers own behavior; EF mapping/migrations own schema; current mobile contract owns public payloads; PostgreSQL proves provider-sensitive behavior.

Acceptance covers target behavior, one counterexample/provider/security/retry case and scope/contract safety. Done requires focused proof, compact v2 evidence, changed-only validation, delivered commit and exact target verification.

Automatic rejection applies to duplicate owners, hidden dependencies, request-supplied identity authority, InMemory-only provider claims, Flutter commands/paths, unguarded/chained commands, mixed lanes, open-ended scope or Done without proof/delivery.

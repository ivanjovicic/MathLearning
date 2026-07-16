# Backend Formal Prompt Contract and Admission Gate

Last aligned: 2026-07-17  
Owner: `backend-agent-system`

Use this file only when creating or materially rewriting a **formal active queue prompt**. A bounded task explicitly assigned by the user does not need queue admission, global deduplication or the long prompt template; start it with `scripts/agent_run.py plan/start` and record its branch/paths.

## Forward-only contract

- Historical unchanged prompt prose stays historical.
- New or materially rewritten non-trivial queue prompts use `Prompt contract: v2`.
- New/promoted claimable rows also use `Prompt admission: v3`.
- Status/evidence-only edits do not trigger migration.

Validate only the changed range:

```powershell
python scripts/validate_agent_prompt.py --changed-from <base-sha>
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
```

Use full/referenced historical evidence scans only for an explicit cleanup task.

## Admission minimum

A prompt becomes `Ready` only when current evidence proves one finite gap, one authoritative owner is named, active owners/visible PRs were checked, dependencies/collisions are explicit, priority has concrete impact, and the work fits one `micro`, `low` or `medium` execution slice. `high` is a finite audit/migration phase, not permission for a multi-owner implementation.

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
Ambiguity rule:
Failure modes:
Execution packet:
Owned paths:
Avoid paths:
Documentation impact:
Acceptance:
Validation:
Completion gate:
Evidence:
```

Repository is `ivanjovicic/MathLearning`. One prompt has one lane from `known-fix`, `investigation`, `validation-only`, `tests`, `docs-evidence`, `audit` or `review`.

## Bounded packet

The packet names exact initial reads, numeric search/edit limits, one hypothesis/falsifier, focused proof within 180 seconds and a stop trigger. Reject whole-repository scans, “fix everything”, unlimited discovery, mixed implementation/migration/audit/review and a medium task expected to touch more than six files.

Backend authority remains unchanged: authenticated server identity owns learner writes; existing services/ledgers own behavior; EF mappings/migrations own schema; current mobile contract owns public payloads; PostgreSQL proves provider-sensitive behavior.

## Completion

Acceptance covers target behavior, one counterexample/provider/security/retry behavior and scope/contract safety. Done requires executed focused proof, compact v2 evidence, changed-only evidence validation, delivered commit and exact target verification when required. `Commit SHA: self` is allowed and is resolved mechanically with `--verify-git`.

Automatic rejection applies to duplicate owners, hidden dependencies, request-supplied identity authority, InMemory-only provider claims, Flutter commands, unguarded/chained commands, mixed lanes, open-ended scope or Done without executable proof and honest delivery.

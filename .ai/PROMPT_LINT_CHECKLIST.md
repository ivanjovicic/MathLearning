# Backend Prompt Contract, Admission and Lint Gate

Last aligned: 2026-07-16  
Owner: `backend-agent-system`

Use this before creating, promoting or materially changing a repository-aware backend prompt. The target is one evidenced problem, one non-duplicate owner, one bounded execution slice, explicit collisions/dependencies and executable proof.

## Contract versions

- Existing unchanged prompt prose may remain historical.
- Every new or materially rewritten non-trivial prompt uses `Prompt contract: v2`.
- Every new active/claimable prompt, or prompt newly promoted to claimable status, also uses `Prompt admission: v3`.
- Status/evidence-only updates do not force migration of otherwise unchanged historical prompts.

Mechanical checks:

```powershell
python scripts/validate_agent_prompt.py docs/prompt_queues/<changed-file>.md
python scripts/validate_agent_system.py
python scripts/validate_agent_evidence.py --referenced-run-logs-only
```

Run commands separately.

## Admission rule

A prompt may become `Ready` only when all are true:

1. Current code, tests, logs or exact contract evidence proves a real gap or finite investigation question.
2. Active queues, current code/completed evidence and visible open PR/branch state were checked for an existing owner.
3. Ownership verdict says new, residual, extension or explicit supersession.
4. One authoritative implementation owner and excluded owners are named.
5. Dependencies, shared paths, migration collisions and safe-parallel boundaries are explicit.
6. Priority names concrete security, correctness, durability, availability or operational impact.
7. Queue placement/order is justified.
8. Scope fits one 15/30-minute execution slice.
9. Focused proof can execute through command policy.
10. Queue row, prompt heading, `Prompt ID` and `Queue` agree.

When one item fails, use `Investigation`, `Blocked`, `Needs validation`, `Ready after <ID>` or another honest non-claimable state.

## Required admission v3 fields

```text
Prompt admission: v3
Problem evidence:
Deduplication check:
Priority rationale:
Dependencies/collisions:
Owner boundary:
Queue placement:
```

`Problem evidence` has at least two concrete items. `Deduplication check` names at least three searched owner sources and the verdict. `Dependencies/collisions` names prerequisites plus overlapping paths/prompts/migrations and the safe-parallel boundary.

## Required v2 fields

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

Repository must be `ivanjovicic/MathLearning`. Allowed lanes are `known-fix`, `investigation`, `implementation`, `tests`, `validation-only`, `docs-evidence`, `audit` and `review`.

## Backend owner boundaries

- Authenticated server identity owns mobile-facing mutation scope; request-body `userId` does not.
- Application/infrastructure services own business behavior; endpoints bind/authenticate/project.
- Existing idempotency ledger/domain table owns duplicate settlement semantics.
- EF Core mappings/migrations own database shape.
- Current mobile contract owns public payload names and retry behavior.
- Provider-sensitive tests use PostgreSQL.
- Logs never own settlement truth and must not expose secrets or full sensitive payloads.

## Failure modes

Select at least two concrete cases; P0/high-risk prompts usually need three or more:

- normal/no-op behavior;
- anonymous/wrong-role/cross-user access;
- duplicate same payload and conflicting duplicate;
- exception, cancellation, rollback or retry;
- concurrent requests/workers;
- stale operation after revoke/reset/restart;
- partial/corrupt persistence;
- schema-from-zero and upgrade behavior;
- safe public error projection;
- unavailable provider/CI proof.

## Execution packet

Include at least:

```text
- Initial reads: exact paths/segments and numeric maximum.
- Search budget: numeric maximum and one question per search.
- First hypothesis/falsifier: confirming and rejecting evidence.
- Expected changed files: exact paths/prefixes and numeric maximum.
- Focused validation target: exact guarded command within 180 seconds.
- Time checkpoints and stop/handoff trigger.
```

Reject whole-repository scans, “fix everything”, unlimited discovery and prompts combining broad audit, implementation, release validation and review.

## Command policy

Follow [`docs/AGENT_COMMAND_PLAYBOOK.md`](../docs/AGENT_COMMAND_PLAYBOOK.md):

```text
one executable per command line
maximum 180 characters
maximum 3 explicit path operands
maximum 180 seconds
no &&, || or semicolon chaining
blocking dotnet/git/network commands use scripts/run_guarded.py
```

A prompt is not Ready when its own commands violate this policy.

## Completion

Acceptance has at least three observable items: target outcome, negative/duplicate/security/retry/provider outcome and scope/contract/safety outcome.

Proof order:

```text
executed focused behavior proof
→ selected counterexample/provider proof
→ structural guard/build/static check
→ wider CI only for a named wider risk
```

Done requires executed proof, honest skipped-check accounting, changed-file safety review, run evidence, committed delivery, exact SHA and synchronized status. When merge/CI is required, verify the delivered target SHA. Placeholder commit text remains non-Done.

## Automatic rejection

Reject/de-promote prompts that duplicate another owner, omit current evidence/impact/collisions, mix unrelated subsystems, allow request-supplied identity as authority, treat InMemory as PostgreSQL proof, contain Flutter paths/commands, use open-ended/chained/unguarded commands, or permit Done without executable evidence and honest delivery.

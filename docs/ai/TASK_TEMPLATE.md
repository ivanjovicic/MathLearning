# Backend Agent Task Template

Use for a new or materially rewritten non-trivial prompt in `ivanjovicic/MathLearning`. Remove placeholders before admission.

```text
# <PROMPT-ID> — <bounded observable outcome>

Prompt contract: v2
Prompt admission: v3                         # required for new/promoted active work
Repository: ivanjovicic/MathLearning
Prompt ID: <PROMPT-ID>
Queue: docs/prompt_queues/<queue>.md
Run lane: known-fix | investigation | implementation | tests | validation-only | docs-evidence | audit | review
Token budget: low | medium | high
Timebox: 15 minutes | 30 minutes

Problem evidence:
- <exact path/symbol/test/log and observed behavior>
- <expected invariant or falsifiable question>

Deduplication check:
- <active queues/router checked and verdict>
- <current code/completed evidence checked and verdict>
- <open PR/branch/claim state checked when visible and verdict>

Priority rationale: P0 | P1 | P2 because <concrete impact>.

Dependencies/collisions:
- <prerequisite or No dependency - reason>
- <conflicting paths/prompts/migration and safe-parallel boundary>

Owner boundary:
- <authoritative implementation/service/ledger/schema owner>
- <explicit excluded owners/non-goals>

Queue placement: <exact queue and order, with reason>.

Task:
<one bounded outcome>

Source of truth:
- current backend code and nearest tests;
- AGENTS.md and exact owning docs;
- mobile contract only when the public API boundary is touched.

Interpretation before work:
- Interpreted outcome: <observable result>
- Assumptions: <none or explicit>
- Expected changed files: <exact paths/prefixes>
- Focused completion proof: <test/validator>

Ambiguity rule:
Stop for unresolved authority, API/schema, auth scope, idempotency/settlement, destructive migration, privacy/security or acceptance ambiguity.

Risk/ownership model:
- <who authoritatively owns writes/identity/schema/contract>
- <what must remain unchanged>

Failure-mode matrix:
- <normal/no-op/idempotent case>
- <auth/cross-user/error/retry/cancel/concurrency/provider case>
- <additional case for P0/high-risk work>

Execution packet:
- Initial reads: <exact paths>; maximum <N>.
- Search budget: maximum <N>; one written question per search.
- First hypothesis/falsifier: <confirming and rejecting evidence>.
- Expected changed files: <exact paths/prefixes>; maximum <N>.
- Validation target: `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test ...`.
- Time checkpoints: minute 5 owner; minute 10 root cause; minute 20 proof; minute 25 closure; minute 30 stop.
- Stop/handoff trigger: budget, second subsystem, second falsifier, unavailable proof or deadline.

Owned paths:
- <exact path/prefix>

Avoid paths:
- <explicit non-goal/path>

Documentation impact:
- updated <owning docs> | none - <specific reason> | follow-up <ID> - <reason>

Acceptance criteria:
1. <target observable behavior>
2. <negative/duplicate/security/retry/provider behavior>
3. <scope/contract/safety behavior>

Proof required:
- <focused executed behavior test>
- <counterexample/provider proof>
- <structural/build/docs supporting proof>

Validation:
- <one command per line, guarded where required>

Completion gate:
Done only with executed target/counterexample proof, honest skipped checks, run evidence, commit SHA, synchronized queue status and required main/CI verification.

Stop conditions:
- second unexpected subsystem or authoritative owner;
- second falsified hypothesis;
- repeated unchanged failure/timeout;
- unavailable required proof;
- time/context/search/edit budget reached.

Evidence:
.ai/runs/<yyyy-mm-dd>-<PROMPT-ID>-evidence.md
```

See [`.ai/PROMPT_LINT_CHECKLIST.md`](../../.ai/PROMPT_LINT_CHECKLIST.md) and [`docs/AGENT_COMMAND_PLAYBOOK.md`](../AGENT_COMMAND_PLAYBOOK.md) before promoting a row to Ready.

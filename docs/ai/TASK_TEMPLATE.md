# Backend Formal Queue Prompt Template

Use only when creating or materially rewriting an active queue prompt. A bounded user-assigned task does not need this ceremony; use `scripts/agent_run.py plan/start` instead.

```text
# <PROMPT-ID> — <bounded observable outcome>

Prompt contract: v2
Prompt admission: v3
Repository: ivanjovicic/MathLearning
Prompt ID: <PROMPT-ID>
Queue: docs/prompt_queues/<queue>.md
Run lane: known-fix | investigation | validation-only | tests | docs-evidence | audit | review
Token budget: micro | low | medium | high
Timebox: 8 | 15 | 30 minutes

Problem evidence:
- <exact path/symbol/test/log and observed behavior>
- <expected invariant/falsifiable question>

Deduplication check:
- <router/active owner verdict>
- <current code/completed evidence verdict>
- <visible PR/branch verdict>

Priority rationale: P0 | P1 | P2 because <impact>.
Dependencies/collisions:
- <prerequisite or none - reason>
- <overlap and safe-parallel boundary>
Owner boundary:
- <authoritative writer>
- <excluded owners/non-goals>
Queue placement: <exact queue/order reason>.

Task: <one outcome>
Source of truth: current code/tests + exact owning docs.
Ambiguity rule: stop for unresolved authority/auth/schema/API/idempotency/privacy/destructive-migration ambiguity.

Failure modes:
- <normal/no-op/idempotent>
- <error/auth/retry/cancel/concurrency/provider>
- <third case for P0>

Execution packet:
- Initial reads: <exact paths>; maximum <N>.
- Search budget: maximum <N>.
- First hypothesis/falsifier: <confirm/reject evidence>.
- Expected changed paths: <paths>; maximum <N>.
- Focused proof: <guarded command, <=180 seconds>.
- Stop: second owner/system, second falsifier, repeated failure, unavailable proof or deadline.

Owned paths:
- <paths>
Avoid paths:
- <paths>
Documentation impact: <updated paths | none - reason | follow-up ID>.

Acceptance:
1. <target behavior>
2. <negative/counterexample/provider behavior>
3. <scope/contract/safety behavior>

Validation:
- <one command per line>

Completion gate: target/counterexample proof + compact v2 log + delivery + changed evidence validation.
Evidence: .ai/runs/<yyyy-mm-dd>-<PROMPT-ID>-evidence.md
```

Do not repeat global mechanics already owned by `.ai/README.md`, `.ai/TOKEN_BUDGETS.md`, `.ai/VALIDATION_SELECTOR.md` or `docs/AGENT_COMMAND_PLAYBOOK.md`.

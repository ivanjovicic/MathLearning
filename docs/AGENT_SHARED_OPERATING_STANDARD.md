# Shared Agent Operating Standard — MathLearning Backend

Last aligned: 2026-07-17  
Scope: `ivanjovicic/MathLearning`

This is the cross-repo minimum. Repository-specific mechanics live in `.ai/README.md`, `AGENTS.md` and their canonical owners. Do not read this document on every task unless cross-repo behavior is involved.

## Minimum rules

1. Current code/tests/tooling override prose and chat memory.
2. One run has one lane, one bounded outcome and one authoritative writer.
3. User-assigned bounded tasks do not require queue discovery/admission.
4. Formal active prompts use v2/v3 admission.
5. Use the mistake index; open the full ledger only to update a card.
6. Use compact v2 evidence with numeric metrics and `Commit SHA: self|<sha>`.
7. Required proof must execute or the state remains non-Done.
8. Docs/audits/prompts are not runtime fixes.
9. Contract changes check the other repo and record sync/defer ownership.
10. Budget breach or failed proof caps completion at 79%.

## Cross-repo impact fields

When auth, API payloads, idempotency, economy, cosmetics, profile, leaderboard or release evidence changes:

```text
Cross-repo impact: yes/no
Other repo checked:
Other repo docs touched:
Deferred sync reason:
Follow-up owner:
```

Backend changes check Flutter contract docs. Flutter changes check backend contract docs. Do not mix runtime edits in both repos into one prompt unless explicitly owned and phased.

## Evidence honesty

```text
Validation run: <exact command/result>
Validation not run: none | not run - <reason>
State: Needs validation | Needs evidence sync | Needs merge | Done | Blocked
```

Queued/in-progress CI is not green. A local focused test is not a substitute for required provider/Actions proof; unrelated red CI is classified rather than opportunistically patched.

## Fast validation

For agent/docs/evidence changes:

```powershell
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
python scripts/analyze_agent_runs.py --changed-from <base-sha> --fail-on-regression
python scripts/validate_agent_system.py
```

Full historical evidence scans are explicit cleanup work, not a default closure gate.

## Final response minimum

Run log; mistake IDs; branch/PR/commit/merge SHA; files changed; validation run/skipped; exact CI/main state; residual risk and next owner.

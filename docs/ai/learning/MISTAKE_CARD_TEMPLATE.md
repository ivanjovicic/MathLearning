# Backend Agent Mistake Card Template

Status: copyable template  
Last aligned: 2026-06-24  
Repo: `ivanjovicic/MathLearning`

Use when a backend run discovers a new mistake or repeats a known one.

Mistake cards belong in:

```text
docs/ai/learning/MISTAKE_LEDGER.md
```

ID format:

```text
BACKEND-MISTAKE-<AREA>-<NNN>
```

## Copyable mistake card

```text
### BACKEND-MISTAKE-<AREA>-<NNN> — <short title>

Severity: P0 | P1 | P2
Status: Open | Mitigated | Watching | Retired | False alarm
First seen:
Repeated in:

Problem:
- What did the agent do wrong?
- What evidence proved it?

Impact:
- Time, tokens, wrong Done status, contract drift, migration risk, idempotency risk?

Root cause:
- Broad prompt, stale queue, missing validation, template unclear, rule not enforced?

Prevention:
- Which rule/prompt/test/queue/log template changed?
- If no change, why not?

Next check:
- What should the next agent verify?
```

## Severity guide (backend)

```text
P0 = settlement/idempotency/auth-scope wrong, false authoritative state, cross-repo contract drift on P0 mutations
P1 = missing evidence, misleading audit, validation claim without dotnet test, queue/evidence mismatch
P2 = stale doc link, redundant file reads, minor checklist gap
```

## Required run-log cross-reference

```text
Relevant prior mistakes read:
- BACKEND-MISTAKE-...

How this run avoids prior mistakes:
- ...

## Mistakes observed

- Mistake ID:
- New or repeated:
- Root cause:
- Prevention added:
- Existing rule that should have prevented it:
- Did this run update a rule/prompt/test/queue:
```

If none:

```text
Mistakes observed: none
```

## Cross-repo block (when touching mobile contracts)

```text
Cross-repo sync: updated | deferred <reason> | not applicable
Mobile docs touched:
- ivanjovicic/Mathlearning-Mobile-App/docs/...
```

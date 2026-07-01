# Agent Run Log Enforcement Gate (Backend)

Last aligned: 2026-06-24  
Status: mandatory closure gate for non-trivial prompts  
Scope: `ivanjovicic/MathLearning`

Model: aligned with `ivanjovicic/Mathlearning-Mobile-App` evidence standards; backend-specific validation and mistake IDs.

## Purpose

Backend agents were completing prompts with commit/test notes but without durable `.ai/runs` logs. That forces later agents to reconstruct work from queue rows, chat, and commit subjects.

This file turns run logging into a hard gate:

```text
No complete run log = no high-confidence Done row.
No classified mistake = no learning-complete run.
```

## Applies when

Use this gate for every non-trivial backend prompt, including:

- endpoint, service, migration, or infrastructure code;
- integration/contract/idempotency tests;
- prompt queue or status edits;
- docs/evidence/audit changes;
- performance or route audits;
- cross-repo contract notes.

Tiny typo-only edits may skip a dedicated `.ai/runs` log only if the completion row says why.

## Start-of-run requirement

Before editing `src/**`, `tests/**`, or broad `docs/**`, create or plan:

```text
.ai/runs/<yyyy-mm-dd>-<prompt-id>-evidence.md
```

Minimum start stub:

```text
Prompt ID:
Queue:
Agent/tool:
Model provider:
Model name/id:
Model mode/settings:
Client/IDE:
Run mode:
Token budget:
Started from queue status:
Local collision check:
Relevant prior mistakes read:
How this run avoids prior mistakes:
```

Use `unknown-not-exposed` for model fields that are not visible. Do not guess.

## Required prompt header (all non-trivial backend prompts)

Every backend queue prompt or ad-hoc task should include:

```text
Relevant prior mistakes read:
- BACKEND-MISTAKE-... (from docs/ai/learning/MISTAKE_LEDGER.md)

How this run avoids prior mistakes:
- ...

Mistakes observed: (fill at end)
```

## Done-row blocker

A prompt must not be marked `Done` unless either:

1. a compact `.ai/runs/<date>-<prompt-id>-evidence.md` log exists and is referenced by the queue row; or
2. the queue row includes `Run log: fallback <reason>` with the same required fields.

A row with only commit SHA, tests, and residual risk must be marked:

```text
Needs evidence sync
Done <percent>% ... Run log: fallback <reason>
```

## Evidence completion score cap

| Situation | Maximum score |
|---|---:|
| Target run log created, referenced, validated | 95–100% |
| Useful audit completed, target run logs still missing | 75% |
| Queue rows updated, no durable `.ai/runs` for target prompts | 70% |
| Model/timing fields missing and not `unknown-*` | 65% |
| Docs/evidence change without path verification | 85% |
| Prompt required logs but only reported missing | 60% |
| Mistake observed but not classified | 80% |
| Repeated mistake without rule/prompt/test/queue update | 75% |
| Docs-only audit claimed as runtime fix | 70% |
| `dotnet test` claimed without command or skip reason | 80% |

Backfill prompts cannot claim 100% if residual risk says target evidence is still missing.

## Mistake learning gate

Before starting, read `docs/ai/learning/MISTAKE_LEDGER.md` and record relevant IDs in the run log.

Before completion, classify every observed mistake as:

```text
new mistake with a mistake card
repeated mistake with a rule/prompt/test/queue update
false alarm with explanation
```

Run log must include:

```text
## Mistakes observed

- Mistake ID:
- New or repeated:
- Root cause:
- Prevention added:
- Existing rule that should have prevented it:
- Did this run update a rule/prompt/test/queue:
```

If none: `Mistakes observed: none`

## Docs-only vs runtime

| Run mode | Allowed completion claim |
|---|---|
| `docs-only`, `docs/audit`, `docs/process bootstrap` | Documentation/process only; no runtime fix |
| `implementation`, `bugfix`, `migration` | Requires code/test evidence in run log |
| `evidence backfill` | Repair logs/queue rows; may not change runtime |

A docs-only audit **cannot** claim a hot-path fix, migration applied, or test pass unless validation proves it.

## Required final fields

See `.ai/RUN_LOG_TEMPLATE.md`. Never leave required fields blank.

Backend validation fields:

```text
Validation run: dotnet test ... --filter "..." — passed
Validation not run: not run - <reason>
CI: No GitHub Actions evidence found via connector
```

## Completion row shape

Every completed queue row should include:

```text
Done <percent>% (YYYY-MM-DD, <agent/lane>, commit `<sha>`)
Model: <provider>/<model or unknown-not-exposed> via <client/tool>
Tests: <exact dotnet test filter or docs-only validation>
Run log: .ai/runs/<yyyy-mm-dd>-<prompt-id>-evidence.md
Mistakes: <BACKEND-MISTAKE-* IDs or none>
Waste: <categories or none recorded>
Missed: <missed work or none known>
Follow-up: <prompt ID or none>
Residual risk: <one sentence>
```

## Self-check before final commit

- run log file is staged;
- owning queue row references the run log;
- mistake fields present;
- docs-only work does not claim runtime fixes;
- validation command is exact or skipped with reason;
- completion score obeys caps above;
- mobile contract impact noted if applicable (`BACKEND-MISTAKE-XREPO-001`).

Suggested checks:

```powershell
git status --short
Select-String -Path .ai/runs/*<prompt-id>* -Pattern "Model provider:","Validation run:","Mistakes observed","Commit SHA:"
```

## If the log is missing after the fact

Use `docs/ai/prompts/BACKEND_EVIDENCE_BACKFILL_PROMPT.md`. Do not invent elapsed time or model names.

## Final response requirement

The final response must mention:

- run-log path;
- mistake IDs added/updated or `none`;
- commit SHA (or uncommitted if user did not request commit);
- validation run or skipped reason;
- biggest waste category;
- residual risk;
- next prompt, if selected.

## Related docs

- `AGENTS.md`
- `docs/BACKEND_CHANGE_CHECKLIST.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `docs/ai/prompts/RUN_LOG_EVIDENCE_LINT_PROMPT.md`

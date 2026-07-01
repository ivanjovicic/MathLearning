# Backend Evidence Backfill Prompt

Use when runtime commits or Done queue rows exist without matching `.ai/runs/<prompt-id>-evidence.md` files.

```text
Use only this repository:
ivanjovicic/MathLearning

Prompt ID:
BACKEND-EVIDENCE-BACKFILL-001

Run mode:
evidence backfill

Token budget:
low–medium

Goal:
Repair missing per-prompt run logs and queue rows for past backend work without changing runtime behavior unless a separate fix prompt is required.

Read first:
- docs/AGENT_RUN_LOG_ENFORCEMENT.md
- docs/ai/learning/MISTAKE_LEDGER.md
- docs/ai/learning/MISTAKE_CARD_TEMPLATE.md
- .ai/runs/README.md
- .ai/RUN_LOG_TEMPLATE.md
- docs/ai/prompts/RUN_LOG_EVIDENCE_LINT_PROMPT.md

Relevant prior mistakes read:
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001 (if contract-related rows)

How this run avoids prior mistakes:
- Backfill only proven facts; use unknown-not-recorded; do not claim live telemetry

Inspect only:
- target prompt IDs or commit SHAs supplied by user/router;
- owning queue rows (BE-PERF-*, BACKEND-CRIT-*, etc.);
- `git log`, `git show --stat` for those commits;
- existing tests/docs referenced by queue Notes;
- do not re-audit entire codebase.

For each target prompt, either:
1. create `.ai/runs/<yyyy-mm-dd>-<prompt-id>-evidence.md` backfill with:
   ```text
   Run mode: evidence backfill
   Elapsed time: unknown-not-recorded
   Phase time breakdown: unknown-not-recorded
   Model name/id: unknown-not-exposed
   Validation run: <from commit message/queue or unknown>
   ```
   using only proven facts; or
2. downgrade queue row to `Needs evidence sync` with reason.

Required work:
- Update queue rows to reference backfill logs or explicit fallback.
- Classify mistakes (usually BACKEND-MISTAKE-EVIDENCE-001 repeated).
- Cap completion at ≤75% if target evidence cannot be verified.
- Do not edit src/** unless user explicitly expands scope.
- Record cross-repo sync status for contract prompts.

Owned paths:
- `.ai/runs/*` backfill files for named prompts only;
- target queue file rows only;
- `docs/ai/learning/MISTAKE_LEDGER.md` (Repeated in / Status only).

Validation:
- git diff --check
- verify every backfilled prompt ID has a matching `.ai/runs` file
- verify backfill logs do not invent model names or timings

Mistakes observed: (fill at end)

Final response:
- prompts/commits backfilled or downgraded;
- queue rows updated;
- mistake IDs;
- validation;
- residual risk (what remains unverifiable);
- commit SHA.
```

## When to run

- After `BACKEND-EVIDENCE-BOOTSTRAP-001` for historical BE-PERF-* rows.
- Before starting `BACKEND-CRIT-001` runtime work if evidence lint finds gaps.
- After any agent session that pushed runtime code without run logs.

## Score cap

Backfill passes that cannot recover validation details:

```text
Done 70–75% ... Run log: .ai/runs/<date>-<prompt-id>-evidence.md (backfill)
Residual risk: original validation details unknown-not-recorded
```

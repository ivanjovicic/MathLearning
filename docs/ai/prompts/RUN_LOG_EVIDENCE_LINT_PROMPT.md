# Run Log Evidence Lint Prompt (Backend)

Use before new backend runtime work when queue rows and `.ai/runs` logs may be out of sync.

```text
Use only this repository:
ivanjovicic/MathLearning

Prompt ID:
BACKEND-RUN-LOG-LINT-001

Run mode:
docs/evidence lint

Token budget:
low

Goal:
Find misleading Done rows, missing run logs, missing model/timing fields, score-cap violations, docs-only audits claimed as fixes, and repeated mistakes before agents continue endpoint/migration/test work.

Read first:
- docs/AGENT_RUN_LOG_ENFORCEMENT.md
- docs/ai/learning/MISTAKE_LEDGER.md
- docs/ai/learning/MISTAKE_CARD_TEMPLATE.md
- .ai/runs/README.md
- .ai/RUN_LOG_TEMPLATE.md
- target owning queue file (e.g. docs/prompt_queues/backend_performance_optimization.md)

Relevant prior mistakes read:
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001

How this run avoids prior mistakes:
- Lint only; no runtime edits; downgrade unclear rows to Needs evidence sync

Inspect only:
- target queue rows supplied by the user or router;
- Done / Done 85%+ / Needs evidence sync / recently changed rows;
- matching `.ai/runs/<date>-<prompt-id>-evidence.md` files;
- mistake IDs in MISTAKE_LEDGER.md relevant to those rows;
- commit metadata only if a row has no run log.

Do not inspect:
- full runtime refactors;
- full commit diffs unless one row references a commit and no run log exists.

Check each completed row:
1. completion percentage exists and obeys score cap;
2. Model/client exists or `unknown-not-exposed`;
3. validation command, docs path check, or `Validation not run` reason exists;
4. `Run log:` path exists or explicit fallback;
5. referenced run-log file exists on disk;
6. run log has model/client, elapsed/phase or `unknown-not-recorded`;
7. run log has waste, missed, follow-up, residual risk, commit SHA;
8. docs-only rows do not claim runtime fixes;
9. repeated mistakes reference BACKEND-MISTAKE-* IDs;
10. repeated mistakes produced rule/prompt/queue update or documented no-op;
11. contract-touching rows record cross-repo sync or flag BACKEND-MISTAKE-XREPO-001.

Required work:
- Small evidence corrections supported by queue rows, run logs, or commit metadata only.
- Mark unclear rows `Needs evidence sync`.
- Update MISTAKE_LEDGER.md for repeated mistakes found.
- Add narrow follow-up (e.g. BACKEND-EVIDENCE-BACKFILL-002) when backfill needs separate pass.
- Do not edit src/** or tests/**.

Owned paths:
- target queue file;
- matching `.ai/runs` files;
- docs/ai/learning/MISTAKE_LEDGER.md;
- optional docs/ai/learning/<yyyy-mm-dd>-run-log-lint.md summary.

Validation:
- git diff --check
- verify each changed Done row references existing run log or fallback
- verify no row claims 100% while residual risk says evidence missing

Mistakes observed: (fill at end)

Final response:
- rows/logs checked;
- corrections made;
- rows needing evidence sync;
- score-cap corrections;
- mistake IDs added/updated;
- validation;
- residual risk;
- commit SHA.
```

## Future automation

When lint keeps finding the same issue, add a docs-only prompt to create `scripts/validate_backend_agent_evidence.py` mirroring Flutter `validate_agent_evidence.py` — queue row creation only; no runtime code in lint pass.

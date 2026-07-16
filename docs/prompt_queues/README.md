# Backend Prompt Queue Router

Last aligned: 2026-07-16  
Repo: `ivanjovicic/MathLearning`

This is the canonical discovery/router document for backend queues. It owns selection order and cross-queue collision visibility; individual queue files own prompt bodies/statuses. State semantics live in [`PROMPT_LIFECYCLE.md`](PROMPT_LIFECYCLE.md).

## Start here

1. Read the exact user-assigned prompt when one exists.
2. Otherwise inspect this router and the highest-priority non-blocked queue.
3. Refresh current code, visible PR/branch state and owning row before starting.
4. Use [`.ai/PROMPT_LINT_CHECKLIST.md`](../../.ai/PROMPT_LINT_CHECKLIST.md) for every new/materially changed prompt.
5. Use one run log per non-trivial prompt.

## Canonical priority order

| Order | Queue | Primary ownership | Collision note |
|---:|---|---|---|
| 1 | [`backend_critical_risk_prevention.md`](backend_critical_risk_prevention.md) | Security, auth, settlement, idempotency and release-blocking integrity | Wins over performance/generic coverage for the same runtime owner. |
| 2 | [`backend_api_db_residuals_pass3_2026_07_16.md`](backend_api_db_residuals_pass3_2026_07_16.md) | Current API/database security/readiness residuals | Check earlier passes and completed evidence before adding IDs. |
| 3 | [`backend_failing_test_followups_2026_07_11.md`](backend_failing_test_followups_2026_07_11.md) | Known migration/test blockers | A proven failing lane outranks a new broad audit. |
| 4 | [`backend_latest_commit_followups_2026_07_11.md`](backend_latest_commit_followups_2026_07_11.md) | Recent implementation closure and exact CI/evidence reconciliation | Close committed work before another latest-commit audit. |
| 5 | [`backend_test_coverage.md`](backend_test_coverage.md) | Canonical test/provider packages | Test owners must not duplicate runtime implementation. |
| 6 | [`backend_performance_followups_2026_07_03.md`](backend_performance_followups_2026_07_03.md) | Performance and bounded operational behavior | Link to canonical runtime/test owner when boundaries overlap. |
| 7 | [`backend_second_pass_risk_prevention.md`](backend_second_pass_risk_prevention.md) | Secondary auth/proxy/job/authoring risks | Start only when higher-priority owners do not overlap. |

The detailed inventory remains in [`docs/DOCS_INDEX.md`](../DOCS_INDEX.md). This router intentionally avoids copying every row/history entry.

## Active-row contract

```markdown
| ID | Priority | Status | Prompt | Purpose |
|---|---:|---|---|---|
| `BACKEND-EXAMPLE-001` | P1 correctness | Ready | `example/BACKEND-EXAMPLE-001.md` | One bounded result and impact. |
```

Statuses follow [`PROMPT_LIFECYCLE.md`](PROMPT_LIFECYCLE.md). `Ready after`, `Needs validation`, `Needs evidence sync`, `Needs merge` and `Blocked` are non-claimable.

## Cross-queue ownership rules

- One runtime behavior, schema/ledger owner or migration chain has one canonical implementation prompt.
- Test/provider/performance/observability prompts support but do not reimplement that owner.
- Auth/user-scope prompts outrank contract convenience for the same route.
- Idempotency/settlement authority outranks performance refactoring for the same mutation.
- Migration/schema-from-zero blockers outrank features depending on that schema.
- Latest-commit validation/evidence closes committed changes before another broad audit.
- Contract-touching prompts check Flutter contract docs and record deferred sync explicitly.

## Admission and validation

```powershell
python scripts/validate_agent_prompt.py docs/prompt_queues/<changed-file>.md
python scripts/validate_agent_evidence.py --referenced-run-logs-only
python scripts/validate_agent_system.py
```

Connector-only changes record these as not run unless an equivalent checked workflow result is inspected. Never infer a pass.

## Archive rule

Completed/superseded prompts leave active tables. Preserve final status, run-log link, commit/merge SHA and residual risk in dated history/archive. Historical evidence does not override current code/tests or the current owning prompt.

# Agent Run Log Enforcement Gate (Backend)

Last aligned: 2026-07-17  
Status: mandatory, compact and changed-scope  
Owner: `backend-agent-system`

## Purpose

Evidence should prevent rediscovery without becoming the task. The old 15-section template, full-ledger pre-read and self-referential SHA backfills added avoidable work. New runs use compact v2, automatic timing and changed-only validation.

```text
No honest target evidence = no Done.
More evidence prose is not more proof.
```

## Applies when

Use a run log for non-trivial runtime/tests/migration/workflow/docs/queue/audit work. Tiny typo-only edits may use `Run log: fallback <reason>`.

## Start/finish mechanically

```powershell
python scripts/agent_run.py plan --area <area> --lane <lane> --budget <budget>
python scripts/agent_run.py start --prompt-id <ID> --queue <queue> --area <area> --lane <lane> --budget <budget>
python scripts/agent_run.py finish .ai/runs/<log>.md --completion <n> --state <state> --inspected <n> --changed <n> --searches <n> --validation-runs <n>
```

The start tool reads [`ai/learning/MISTAKE_INDEX.json`](ai/learning/MISTAKE_INDEX.json). Do not scan the full ledger unless adding/updating a card.

## Compact v2 requirements

Canonical shape: [`.ai/RUN_LOG_TEMPLATE.md`](../.ai/RUN_LOG_TEMPLATE.md).

Required evidence groups:

1. identity/lane/budget/timestamps;
2. owner/hypothesis and selected mistake IDs;
3. numeric throughput metrics;
4. observable outcome and changed paths;
5. exact validation run/skipped;
6. exceptions/learning;
7. delivery state, `self|SHA` and completion.

Target 35–70 lines; warning above 90. Use `none` instead of empty narrative sections.

## Commit SHA without cleanup commits

`Commit SHA: self` means the latest commit containing that log revision. It is resolved by:

```powershell
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
```

`Commit SHA: self` is the default. The validator resolves the commit that introduced the log. The v2 log is an immutable execution snapshot: PR/main delivery is verified from repository history, so do not create a second commit only to replace the SHA or rewrite the pre-merge state.

## Changed-only validation

Current work must use:

```powershell
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
python scripts/analyze_agent_runs.py --changed-from <base-sha> --fail-on-regression
```

This validates changed queue lines and changed/referenced logs only. Legacy backlog is handled by an explicit full-audit/backfill task and cannot block unrelated work.

## Completion caps

| Situation | Maximum |
|---|---:|
| Focused target/counterexample proof, compact log, delivered/verified | 95–100% |
| Required proof failed/unavailable | 79% |
| Budget/read/search/change limit exceeded | 79% |
| Useful docs/audit output but no runtime proof | 75% |
| Runtime commit lacks target log/status sync | 70% |
| Mistake observed but unclassified | 80% |
| Repeated mistake without `prevention=<change>` | 75% |
| Docs-only work described as runtime fix | 70% |

`Done` requires completion >=95. `100%` requires `Residual risk: none` (or equivalent no-material-risk wording).

## One lane only

Allowed v2 lanes:

```text
known-fix | investigation | validation-only | tests | docs-evidence | audit | review
```

Values such as `implementation + migration/bootstrap/readiness` are invalid. Split the work.

## Compact queue row

Do not duplicate the whole log in a queue cell. Required Done tail:

```text
Done <percent>% — Run log: .ai/runs/<log>.md; Validation: <focused result>; Residual risk: <sentence>; Commit: self|<sha>
```

Model, waste, missed work and follow-up live in the linked log.

## Mistake learning

- `MISTAKE_INDEX.json` routes normal tasks to relevant IDs.
- `MISTAKE_LEDGER.md` remains the detailed owner.
- `Mistakes observed: none` is valid.
- A repeated mistake uses `BACKEND-MISTAKE-* repeated; prevention=<rule/test/tool change>`.
- Use `scripts/analyze_agent_runs.py` to measure unknown timing, mixed lanes, oversized logs and repeated waste.

## CI scope

Docs/agent-tooling-only changes are proved by Backend Agent System Validation and skip the expensive database suite. Runtime/test/schema/build changes still run PostgreSQL schema, full tests, coverage, migration artifact and readiness smoke.

## Final response

Mention the run log, mistakes, branch/PR/commit/merge SHA, files changed, validation, exact CI/main state, residual risk and next owner. Do not reproduce the entire log.

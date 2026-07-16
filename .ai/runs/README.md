# Backend Agent Run Logs

Run logs are durable, compact evidence—not a second project diary.

## Fast start and close

```powershell
python scripts/agent_run.py plan --area <area> --lane <lane> --budget <budget>
python scripts/agent_run.py start --prompt-id <ID> --queue <queue> --area <area> --lane <lane> --budget <budget>
python scripts/agent_run.py finish .ai/runs/<log>.md --completion <n> --state <state> --inspected <n> --changed <n> --searches <n> --validation-runs <n>
```

Naming remains:

```text
.ai/runs/<yyyy-mm-dd>-<prompt-id>-evidence.md
```

New logs use `Evidence format: v2` from [`../RUN_LOG_TEMPLATE.md`](../RUN_LOG_TEMPLATE.md). Historical logs remain valid legacy evidence and are not reformatted by default.

## What the next agent needs

A good log answers five questions quickly:

1. What changed and who owned it?
2. What exact proof ran or failed?
3. What was delivered and where?
4. What mistake/waste should not repeat?
5. What residual work has an owner?

## Validation

For the current task, validate changed rows/logs only:

```powershell
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
python scripts/analyze_agent_runs.py --changed-from <base-sha> --fail-on-regression
```

Use the manual/full evidence workflow only for an intentional historical cleanup. Do not let legacy debt block an unrelated current run.

## Learning

Use [`../../docs/ai/learning/MISTAKE_INDEX.json`](../../docs/ai/learning/MISTAKE_INDEX.json) to select relevant cards. Open the full ledger only when updating a card or investigating a newly repeated pattern.

`Commit SHA: self` is valid and is resolved from Git history. Backfill-only SHA commits are obsolete.

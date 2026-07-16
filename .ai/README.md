# MathLearning Backend AI Workflow Entrypoint

Last aligned: 2026-07-17  
Owner: `backend-agent-system`

This is the default low-context entrypoint. It routes one decision at a time and deliberately avoids making agents reread the full workflow, queue history or mistake ledger.

## 60-second fast start

For a user-assigned bounded task, do not create or search for another queue prompt. The user assignment plus the task branch/PR is the owner.

```powershell
python scripts/agent_run.py plan --area <area> --lane <lane> --budget <micro|low|medium|high>
python scripts/agent_run.py start --prompt-id <ID> --queue user-assigned --area <area> --lane <lane> --budget <budget>
```

The tool selects relevant `BACKEND-MISTAKE-*` IDs from [`docs/ai/learning/MISTAKE_INDEX.json`](../docs/ai/learning/MISTAKE_INDEX.json), prints the minimal read/proof packet and creates a compact timed run log. Read the full ledger only when adding or changing a mistake card.

For a normal implementation task, the default read set is:

1. exact user request or queue row;
2. relevant `AGENTS.md` section;
3. target source segment and nearest focused test;
4. one mapped owning document;
5. only the mistake cards selected by the index.

Do not chain-read workflow documents.

## Fast execution model

```text
ASSIGN → PLAN → OWNER/HYPOTHESIS → PATCH → FOCUSED PROOF → DELIVER → CLOSE
```

Before editing, know only these values:

```text
Outcome:
Authoritative owner:
First hypothesis/falsifier:
Expected changed paths and limit:
Focused proof:
Stop/handoff trigger:
Delivery target:
```

Use [`TOKEN_BUDGETS.md`](TOKEN_BUDGETS.md). A second subsystem, second falsified hypothesis, repeated unchanged failure or budget breach stops implementation.

## User-assigned vs formal queue work

- **User-assigned bounded task:** no queue discovery/admission ceremony; record `Queue: user-assigned` and work on one branch/PR.
- **Existing queue task:** read only the owning row and linked prompt.
- **Creating/promoting a new active queue prompt:** use [`.ai/PROMPT_LINT_CHECKLIST.md`](PROMPT_LINT_CHECKLIST.md) and admission v3.
- **Audit:** findings only; split implementation into bounded owners.

## Canonical owners

| Need | Read |
|---|---|
| Backend invariants | [`../AGENTS.md`](../AGENTS.md) |
| Rule ownership | [`SOURCE_OF_TRUTH.md`](SOURCE_OF_TRUTH.md) |
| Time/read/search/edit limits | [`TOKEN_BUDGETS.md`](TOKEN_BUDGETS.md) |
| Validation selection | [`VALIDATION_SELECTOR.md`](VALIDATION_SELECTOR.md) |
| Command timeouts | [`../docs/AGENT_COMMAND_PLAYBOOK.md`](../docs/AGENT_COMMAND_PLAYBOOK.md) |
| Queue selection/state | [`../docs/prompt_queues/README.md`](../docs/prompt_queues/README.md), [`../docs/prompt_queues/PROMPT_LIFECYCLE.md`](../docs/prompt_queues/PROMPT_LIFECYCLE.md) |
| Compact evidence | [`RUN_LOG_TEMPLATE.md`](RUN_LOG_TEMPLATE.md), [`../docs/AGENT_RUN_LOG_ENFORCEMENT.md`](../docs/AGENT_RUN_LOG_ENFORCEMENT.md) |
| Mistake routing | [`../docs/ai/learning/MISTAKE_INDEX.json`](../docs/ai/learning/MISTAKE_INDEX.json) |

## Focused validation

Choose proof through [`VALIDATION_SELECTOR.md`](VALIDATION_SELECTOR.md). Default order:

```text
reproducer/current contract
→ nearest focused behavior and counterexample
→ provider/build/static proof only when required
→ changed docs/prompt/evidence checks
→ full suite only for a named wider risk
```

Changed evidence is validated without scanning historical debt:

```powershell
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
python scripts/analyze_agent_runs.py --changed-from <base-sha> --fail-on-regression
```

## Compact closure

Finish the log mechanically instead of manually writing 15 empty sections:

```powershell
python scripts/agent_run.py finish .ai/runs/<log>.md `
  --completion <0-100> --state <state> --inspected <n> --changed <n> `
  --searches <n> --validation-runs <n> --outcome "<result>" `
  --changed-path <path> --validation "<command/result>" --branch-pr "<branch/PR>"
```

`Commit SHA: self` is the canonical no-backfill sentinel. CI resolves it from Git history, so a second “record commit SHA” commit is not required.

## CI routing

`Database Validation` classifies changed paths first. Docs/agent-tooling-only changes skip PostgreSQL/full-suite work and still complete the `validate-database` gate. Runtime, test, migration, solution and DB-script changes run the full suite.

## Stop rules

Stop and hand off when authority is unclear, required proof cannot run, the task crosses into a second subsystem, the same failure repeats after one changed retry, or the selected budget is reached. Do not continue merely to make a checklist look complete.

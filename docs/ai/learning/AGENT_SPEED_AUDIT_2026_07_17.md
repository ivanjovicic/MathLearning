# Backend Agent Speed Audit — 2026-07-17

Status: implemented process/tooling improvement  
Scope: learning logs, mistake routing, evidence, prompt lifecycle and CI selection

## Finding summary

| Bottleneck | Evidence observed | Impact | Implemented response |
|---|---|---|---|
| Full mistake-ledger read | Rulebook/enforcement pointed every run at the full ledger | Hundreds of repeated context lines before the target file | Added `MISTAKE_INDEX.json` and `agent_run.py plan`; full ledger is update-only |
| Manual oversized evidence | Old template had many separate empty sections and target up to 120 lines | Closure work competes with implementation time | Compact v2, automatic start/finish/timing, 35–70 line target |
| Unknown task duration | Repository search found many logs with `Elapsed time: unknown-not-recorded` | No measurable throughput or deadline learning | UTC start/finish and computed elapsed time |
| SHA backfill loop | Multiple commits existed only to record/fix run-log SHAs | Extra commits, rebases and evidence sync | `Commit SHA: self` resolved from Git history |
| Historical validator noise | `--referenced-run-logs-only` still scanned every queue row first | Unrelated legacy debt blocks current closure | `--changed-from` validates changed queue lines and changed/referenced logs only |
| Mixed/oversized task scope | Example medium run used a mixed implementation+migration/bootstrap lane, inspected ~30 files and changed >20 | More hypotheses, retries, docs and review/CI surface | One-lane validator, micro budget, hard file caps and split rule |
| Full DB CI on docs-only PR | Database workflow ran PostgreSQL, restore/build/schema/full tests for agent docs/Python changes | Minutes of wait plus unrelated pre-existing red tests | Changed-path classifier, conditional DB suite and final always-present gate |
| Queue-row duplication | Done rows copied model/waste/missed/follow-up details already in logs | Large tables and repeated edits | Compact Done tail links to v2 log |

## New fast path

```text
plan/start (<60s)
→ 2–5 targeted reads
→ patch
→ focused proof
→ finish log (<3m)
→ changed evidence/system checks
→ PR/main delivery
```

## Mechanical gates added

- compact log generator/closer with automatic timing;
- mistake-area router;
- changed-line evidence validator;
- self-SHA Git verification;
- budget, one-lane, failed-validation and residual-risk enforcement;
- run-speed analyzer;
- CI scope classifier with tests;
- agent-system workflow coverage for all new tools;
- docs-only database-suite skip with a stable final check.

## Expected effect

- Small docs/tooling task: target 8 minutes instead of a 15–30 minute full ceremony.
- Known bug: first edit by half of the selected budget.
- Evidence closure: under 3 minutes with no SHA cleanup commit.
- Current-task validation: no historical evidence backlog scan.
- Docs/agent-only PR: no PostgreSQL/full .NET suite wait.
- Oversized multi-owner work: split before code rather than discovered during validation.

## Safety retained

The optimization does not weaken runtime proof. Runtime/test/schema/build changes still execute the full database workflow. Failed required validation remains non-Done and capped at 79%. Provider-sensitive, auth, idempotency and migration rules are unchanged.

# Compact Run Log Template (Backend v2)

Create logs with `scripts/agent_run.py start`; close them with `scripts/agent_run.py finish`. Manual copy is a fallback only.

```text
# <PROMPT-ID> Evidence

Evidence format: v2
Prompt ID: <PROMPT-ID>
Queue: user-assigned | docs/prompt_queues/<queue>.md
Agent/tool: <tool>
Model provider: <provider | unknown-not-exposed>
Model name/id: <model | unknown-not-exposed>
Client/IDE: <client | unknown-not-exposed>
Run mode: known-fix | investigation | validation-only | tests | docs-evidence | audit | review
Token budget: micro | low | medium | high
Started at UTC: <ISO-8601>
Completed at UTC: <ISO-8601>
Elapsed time: <duration>
Relevant prior mistakes read: <IDs selected by MISTAKE_INDEX.json | none>
How this run avoids prior mistakes: <one compact prevention statement>
Owner/hypothesis: <authoritative owner and falsifiable hypothesis>
Files inspected: <n>
Files changed: <n>
Searches: <n>
Validation runs: <n>
Failed retries: <n>

## Outcome
- <observable result, maximum 3 bullets>

## Changed paths
- <only changed paths; use none for validation/review-only>

## Validation
Validation run: <exact command/result | none>
Validation not run: <none | not run - reason>

## Exceptions and learning
Mistakes observed: none | <BACKEND-MISTAKE-ID new/repeated; prevention=<change>>
Waste: none | <short categories>
Missed: none | <bounded missing work>
Follow-up: none | <existing/new owner>
Residual risk: none | <one sentence>
Documentation impact: none - <reason> | updated <paths> | follow-up <ID>
Cross-repo impact: no | yes - <checked/touched/deferred>

## Delivery
State: In progress | Needs validation | Needs evidence sync | Needs merge | Blocked | Done | Archived
Branch/PR: <branch and PR | direct main | none>
Commit SHA: self | <real SHA>
Completion %: <0-100>
```

## Why `self` is allowed

A log cannot know the SHA of the commit that contains it. `Commit SHA: self` means “the latest Git commit that contains this log revision”. `validate_agent_evidence.py --verify-git` resolves it mechanically. Do not create a cleanup commit only to replace `self` with a hash. The log is an immutable execution snapshot; final PR/main delivery is verified from GitHub history, not by rewriting pre-merge fields.

## Compactness rules

- Target 35–70 lines; warning above 90.
- Do not paste full diffs, logs, test output or secrets.
- Do not create empty narrative sections. Use `none`.
- List changed paths, not every file inspected; record inspection as a count plus owner/hypothesis.
- One run mode only. Split mixed implementation/migration/audit/review work.

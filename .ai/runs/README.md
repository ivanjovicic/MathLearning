# Backend Agent Run Logs

Status: mandatory post-prompt evidence index  
Last aligned: 2026-06-24  
Repo: `ivanjovicic/MathLearning`

## Purpose

This folder stores compact logs for AI-agent runs on the MathLearning **backend** repo.

A run log is durable memory that helps the next agent spend fewer tokens, avoid repeated mistakes, and continue from real evidence instead of chat history or queue rows alone.

For enforcement, also read:

- `../RUN_LOG_TEMPLATE.md`
- `../../docs/AGENT_RUN_LOG_ENFORCEMENT.md`
- `../../docs/ai/learning/MISTAKE_LEDGER.md`

## File naming

Use:

```text
.ai/runs/<yyyy-mm-dd>-<prompt-id>-evidence.md
```

Examples:

```text
.ai/runs/2026-06-24-BACKEND-CRIT-001-evidence.md
.ai/runs/2026-06-24-be-perf-004-evidence.md
.ai/runs/2026-06-24-ad-hoc-idempotency-fix-evidence.md
```

## Fast start

For non-trivial work, copy:

```text
.ai/RUN_LOG_TEMPLATE.md
```

into:

```text
.ai/runs/<yyyy-mm-dd>-<prompt-id>-evidence.md
```

Fill it before the completion commit.

Do not leave required fields blank. Use:

```text
unknown-not-exposed
unknown-not-recorded
none
not run - <reason>
```

## Required shape

Each log must include the fields in `../RUN_LOG_TEMPLATE.md`, especially:

```text
Relevant prior mistakes read:
How this run avoids prior mistakes:
Mistakes observed:
Validation run:
Validation not run:
Completion %:
Residual risk:
Commit SHA:
```

## Backend-specific rules

- **Runtime code** under `src/**` or `tests/**` without a matching run log must be backfilled or the queue row marked `Needs evidence sync`.
- **Docs-only audits** must not claim runtime fixes; `Run mode` should say `docs-only` or `docs/audit`.
- **dotnet test** claims must name the exact filter/command or mark validation `not run - <reason>`.
- **Cross-repo contract** changes must note whether `ivanjovicic/Mathlearning-Mobile-App` docs were synced or deferred (`BACKEND-MISTAKE-XREPO-001`).
- Do not claim CI green without Actions evidence; write `No GitHub Actions evidence found via connector`.

## Completion blocker

A completed queue row must reference its run log:

```text
Run log: .ai/runs/<yyyy-mm-dd>-<prompt-id>-evidence.md
```

If a run log cannot be created:

```text
Run log: fallback <reason>
```

Plain `Done` without a run log or fallback is not enough for non-trivial work.

## Compactness rule

Target: 40–120 lines. No full diffs, no pasted `dotnet test` output, no secrets.

## Learning rule

When a waste or mistake category repeats:

1. update a rule in `AGENTS.md` or `docs/BACKEND_*`;
2. update a queue row or `docs/ai/prompts/*`;
3. add a mistake card to `docs/ai/learning/MISTAKE_LEDGER.md`;
4. or record why no change was needed in the run log.

After 5 meaningful run logs or 3 repeated mistake categories, run `docs/ai/prompts/AGENT_MISTAKE_ROLLUP_PROMPT.md`.

## Do not include

- connection strings, JWT secrets, passwords, refresh tokens;
- full request/response bodies with PII;
- speculative claims not backed by files or commands.

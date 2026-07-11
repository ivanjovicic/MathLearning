# BACKEND-FAILING-TESTS-001 Evidence

Prompt ID: BACKEND-FAILING-TESTS-001
Queue: `docs/prompt_queues/backend_test_coverage.md` and `docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md`
Agent/tool: ChatGPT via GitHub connector and GitHub Actions
Model provider: OpenAI
Model name/id: GPT-5.6 Thinking
Model mode/settings: reasoning, repository editing
Client/IDE: ChatGPT connector session
Run mode: validation-first, minimal repair
Token budget: high
Started from queue status: ad-hoc user request following BACKEND-COVERAGE-EXPANSION-001
Local collision check: active central and latest follow-up queues inspected before allocating this prompt ID
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-VALIDATION-002, BACKEND-MISTAKE-QUEUE-001
How this run avoids prior mistakes: create evidence before edits; classify every failure from executable TRX evidence; preserve canonical contracts; add queue prompts for unsafe or broader residual work; do not claim full-suite green without checked workflow evidence
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Starting evidence

- Starting branch: `main`
- Working branch: `agent/fix-failing-tests-2026-07-11`
- Source artifact: GitHub Actions diagnostic run `29145382740`, artifact `8246596984`
- Full-suite result at start: 960 passed, 35 failed, 995 total
- Failure groups at start:
  - 30 SQLite relational setup failures with `SQLite Error 1: near "AT": syntax error`
  - 2 stale safe-error expectations
  - 1 AdminApiClient actionable-message assertion
  - 1 idempotency observability authorization metadata assertion
  - 1 linear-equation step-engine result assertion

## Files inspected

- pending

## Files changed

- this evidence file

## Validation run

- pending

## Validation not run

- pending

## What was done

- Parsed the retained TRX/test log and grouped all 35 failures by root-cause signature.

## What was missed

- Runtime/test repairs are in progress.

## Waste categories

- connector-only repository access
- prior standard workflow stopped before the full test suite

## Mistakes observed

- pending classification

## Follow-up prompt

- pending

## Completion %

- 10%

## Residual risk

- Provider-specific failures may require a dedicated PostgreSQL lane or provider-aware EF model strategy rather than local assertion edits.

## Commit SHA

- pending

## Cross-repo sync

Cross-repo impact: no expected mobile contract change.
Other repos checked: none yet.
Other repo docs touched: none.
Deferred sync reason: no mobile payload or behavior change is planned.
Follow-up prompt: pending.

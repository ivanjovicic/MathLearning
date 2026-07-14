# BACKEND-LATEST-WORKFLOW-002 Evidence

Prompt ID: BACKEND-LATEST-WORKFLOW-002
Queue: docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md
Agent/tool: Codex desktop + GitHub app
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: Codex desktop
Run mode: GitHub Actions validation and artifact review
Token budget: unknown-not-exposed
Elapsed time: unknown-not-recorded
Phase time breakdown: GitHub run lookup 00:00:08; job/log/artifact review 00:00:12; queue/evidence update 00:00:05
Started from queue status: Prompt-ready
Local collision check: git status already dirty with existing user/agent changes; no new collision introduced yet
Relevant prior mistakes read:
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001
- BACKEND-MISTAKE-QUEUE-001
How this run avoids prior mistakes:
- bind the exact `main` SHA to the observed workflow run and artifact state before updating queue rows
- record the actual workflow conclusion and failure class instead of inferring success from local build/test results
- keep workflow evidence separate from local validation evidence

## Files inspected

- `.github/workflows/database-validation.yml`
- `.ai/runs/2026-07-13-BACKEND-LATEST-VALIDATION-002-evidence.md`
- `docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md`
- `docs/prompt_queues/backend_test_coverage.md`

## Files changed

- `.ai/runs/2026-07-13-BACKEND-LATEST-WORKFLOW-002-evidence.md`
- `docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md`

## Commands run

- `git ls-remote origin refs/heads/main`
- `Invoke-RestMethod` to `https://api.github.com/repos/ivanjovicic/MathLearning/actions/workflows/database-validation.yml/runs?head_sha=9b01a629e7571375986d85dce8075652fc680ad8&per_page=10`
- GitHub app `_fetch_workflow_run_jobs` for run `29150275641`
- GitHub app `_fetch_workflow_job_logs` for job `86538554346`
- GitHub app `_fetch_workflow_run_artifacts` for run `29150275641`

## What was done

- Verified the exact `main` SHA is `9b01a629e7571375986d85dce8075652fc680ad8`.
- Found one `Database Validation` workflow run for that SHA: `https://github.com/ivanjovicic/MathLearning/actions/runs/29150275641`.
- Read the failed job log and captured the failing schema-from-zero step.
- Confirmed no workflow artifacts were published.
- Updated the queue row to reflect validation failure instead of leaving it prompt-ready.

## What was missed

- No remote rerun was triggered from this thread.
- No code or workflow file was edited because the failure proof was sufficient for classification, but the safe push/rerun path was not available here.

## Validation run

- Workflow run `29150275641` completed with conclusion `failure`.
- Job `validate-database` failed at step `Validate schema from zero`.
- The failure was `Npgsql.PostgresException 42704`: constraint `"FK_user_avatar_configs_UserProfiles_UserId"` on `user_avatar_configs` does not exist.
- `Run test suite with coverage` was skipped.
- `Generate coverage summary` failed because `artifacts/test-results` did not exist.
- `Generate idempotent artifact` was skipped.
- `Smoke startup schema guard` was skipped.
- `Upload test and coverage artifacts` produced no artifacts.

## Validation not run

- No successful workflow rerun.
- No full coverage artifact review because the workflow stopped before producing artifacts.

## Waste categories

- Workflow/schema mismatch on a clean database.
- Artifact absence after early migration failure.
- Time spent separating local build/test success from remote workflow failure.

## Mistakes observed

Mistakes observed: none

## Where time/context was wasted

- The workflow failure appears only in clean schema-from-zero CI, so local build/test success was not enough to prove the database path.

## Why waste happened

- The workflow exposes a schema history issue that does not surface in the local focused test slice.

## What the next agent should avoid

- Treating a green local build/test as proof that the clean-database workflow is healthy.
- Ignoring the first failing migration step when later steps are skipped.

## Docs/rules updated to prevent repeat

- `docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md`

## Queue updated

- `BACKEND-LATEST-WORKFLOW-002` marked `Validation failed` with schema-from-zero failure class and no artifact output.

## New optimized prompt added

- None.

## Follow-up prompt

BACKEND-LATEST-EVIDENCE-002

## Completion %

75%

## Residual risk

The `Database Validation` workflow still fails on the reviewed `main` SHA because schema-from-zero migration `20260624133144_AlignCosmeticsMobileDataModel` cannot drop the expected `user_avatar_configs` foreign key on a clean database, and no artifacts are produced.

Commit SHA: 9b01a629e7571375986d85dce8075652fc680ad8

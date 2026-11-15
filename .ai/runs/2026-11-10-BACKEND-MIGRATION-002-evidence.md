# BACKEND-MIGRATION-002 Evidence

Evidence format: v2
Prompt ID: BACKEND-MIGRATION-002
Queue: formal
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: tests
Token budget: medium
Started at UTC: 2026-11-10T11:13:20Z
Completed at UTC: 2026-11-10T14:54:12Z
Elapsed time: 220m 52s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-CI-001, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-AUTH-001; apply BACKEND-MISTAKE-CI-001; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: Seven critical UserAnswerAudits columns need exact physical type, nullability, and length validation at startup; if a wrong-shaped same-name column still reports Ready, current guard fails the target.
Files inspected: 11
Files changed: 5
Searches: 4
Validation runs: 22
Failed retries: 2

## Outcome
- Implemented exact PostgreSQL shape checks for seven critical UserAnswerAudits columns. Startup CheckAsync now reports Mismatch for wrong type, nullability and length; valid migrated schema remains Ready. CI provider gate was aligned so these regressions execute under POSTGRES_PROVIDER_TESTS_REQUIRED.

## Changed paths
- src/MathLearning.Api/Services/DatabaseSchemaVersionGuard.cs;tests/MathLearning.Tests/Infrastructure/MigrationDriftRegressionTests.cs;docs/migration-drift-issue-47.md;docs/prompt_queues/backend_migration_shape_guard.md;.ai/runs/2026-11-10-BACKEND-MIGRATION-002-evidence.md

## Validation
Validation run: PASS: guarded restore; PASS: strict Release test-project build with analyzers and code style enforced, 0 warnings/errors; RED: pre-change PostgreSQL cases accepted wrong type, required-nullable and wrong-length columns; PASS: post-change MigrationDriftRegressionTests with POSTGRES_PROVIDER_TESTS_REQUIRED=1, 22/22; PASS: isolated PostgreSQL 18 schema-from-zero plus DatabaseSchema tests, 7/7; PASS: EF migrations has-pending-model-changes, no pending model changes; PASS: git diff --check; PASS: changed documentation/evidence validators.
Validation not run: dotnet format was not rerun because the previous code-analysis cycle timed out on existing WHITESPACE diagnostics; full solution suite was outside this bounded migration owner; PostgreSQL 16 CI result is pending.

## Exceptions and learning
Mistakes observed: BACKEND-MISTAKE-VALIDATION-001: initial build-including focused test exceeded the guarded timeout; split build and no-build tests produced exact red/green proof. BACKEND-MISTAKE-CI-001: aligned guard-provider test gate with the workflow variable POSTGRES_PROVIDER_TESTS_REQUIRED.
Waste: A malformed -m switch failed before running tests; removed it. Do not repeat the unchanged formatter check from the previous analysis cycle.
Missed: The task exceeded its 30-minute prompt timebox; queue remains Needs validation at 79% despite green local proof.
Follow-up: backend-agent-system: review the timebox exception and synchronize the CI outcome; do not reopen runtime scope unless CI finds a failure.
Residual risk: No known local functional failure. The formal 30-minute run budget was exceeded; PostgreSQL 16 CI remains pending after delivery.
Documentation impact: Updated docs/migration-drift-issue-47.md with exact column-shape startup behavior and updated the migration-shape queue row with proof and residual state.
Cross-repo impact: None.

## Delivery
State: Needs validation
Branch/PR: PR #53 https://github.com/ivanjovicic/MathLearning/pull/53; head 3462922; local proof complete; CI pending; merge pending.
Commit SHA: self
Completion %: 79

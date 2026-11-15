# BACKEND-ANALYSIS-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-ANALYSIS-001
Queue: formal
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: audit
Token budget: medium
Started at UTC: 2026-11-10T09:58:08Z
Completed at UTC: 2026-11-10T10:24:28Z
Elapsed time: 26m 20s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-CI-001, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-AUTH-001; apply BACKEND-MISTAKE-CI-001; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: EF Core mappings, migration snapshot and startup schema guard remain aligned and fail closed; any mismatch, unsafe history editing or unbounded startup probe falsifies this.
Files inspected: 12
Files changed: 5
Searches: 8
Validation runs: 7
Failed retries: 1

## Outcome
- Completed the EF Core/startup rotation. Release analyzer build passed with 0 warnings/errors; restore, migration drift regressions (18/18), and vulnerable-package inventory passed. Static risk: startup checks seven UserAnswerAudits critical columns by name only, so post-migration type/length/nullability drift may be reported Ready. Routed to BACKEND-MIGRATION-002; no runtime change in this audit.

## Changed paths
- docs/prompt_queues/BACKEND-MIGRATION-002-critical-column-shape-guard.md;docs/prompt_queues/backend_migration_shape_guard.md;docs/prompt_queues/README.md;docs/prompt_queues/backend_code_analysis.md;.ai/runs/2026-11-10-BACKEND-ANALYSIS-001-evidence.md

## Validation
Validation run: PASS: guarded restore; PASS: strict Release analyzer build, 0 warnings/errors; PASS: EF pending-model check, no pending model changes (first attempt used wrong config); PASS: MigrationDriftRegressionTests 18/18; PASS: vulnerable package inventory, none; PASS: prompt validator; PASS: full documentation health, 28 documents/0 failures; PASS: git diff --check; TIMEOUT: focused dotnet format after existing WHITESPACE diagnostics.
Validation not run: Local PostgreSQL/schema-from-zero proof and Neon telemetry were unavailable; no production database inspected.

## Exceptions and learning
Mistakes observed: BACKEND-MISTAKE-VALIDATION-001: EF CLI first resolved Debug metadata after a Release-only build; reran with --configuration Release.
Waste: Do not retry unchanged dotnet format; the run timed out after existing WHITESPACE diagnostics and the previous rotation had the same baseline.
Missed: No live PostgreSQL physical-schema proof in this environment.
Follow-up: Execute BACKEND-MIGRATION-002 with a disposable PostgreSQL target and test-first proof.
Residual risk: Formatter baseline remains unresolved; PostgreSQL takeover/physical-schema proof remains unavailable; startup column shape risk is static only until the routed PostgreSQL regression is run.
Documentation impact: Added a bounded formal migration-shape prompt and queue row, indexed it in the prompt router, updated the code-analysis queue, and recorded evidence.
Cross-repo impact: None.

## Delivery
State: Needs validation
Branch/PR: PR #52 https://github.com/ivanjovicic/MathLearning/pull/52; CI checks passed on submitted head 92a489d and database-suite was skipped as docs-only; merge pending.
Commit SHA: self
Completion %: 79

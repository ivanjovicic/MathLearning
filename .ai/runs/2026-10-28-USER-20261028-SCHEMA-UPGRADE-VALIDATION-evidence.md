# USER-20261028-SCHEMA-UPGRADE-VALIDATION Evidence

Evidence format: v2
Prompt ID: USER-20261028-SCHEMA-UPGRADE-VALIDATION
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: validation-only
Token budget: high
Started at UTC: 2026-10-28T17:29:52Z
Completed at UTC: 2026-10-28T17:45:54Z
Elapsed time: 16m 2s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-CI-001, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-AUTH-001; apply BACKEND-MISTAKE-CI-001; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: docs/database-migrations.md and the EF-generated idempotent SQL own migration preparation; the script should upgrade and replay safely from the stated baseline, falsified by a PostgreSQL error or failed schema assertion.
Files inspected: 9
Files changed: 1
Searches: 3
Validation runs: 12
Failed retries: 4

## Outcome
- EF idempotent output matched the checked-in SQL; PostgreSQL 18 isolated baseline upgrade, replay, and schema assertions passed; fresh-schema guard passed 1/1; docs and agent-system checks passed. Production preflight remains outstanding.

## Changed paths
- .ai/runs/2026-10-28-USER-20261028-SCHEMA-UPGRADE-VALIDATION-evidence.md

## Validation
Validation run: FreshMigratedPostgresDatabase_PassesSchemaGuard on isolated PostgreSQL 18: 1/1 pass. | EF migration script generation and checked-in SQL comparison: exact match. | Generated baseline plus checked-in idempotent upgrade executed twice; migration-history/index/column assertion: true. | check_documentation_health.py --full-links: 28 documents, 0 failures. | validate_agent_system.py: 0 failures; git diff --check: pass.
Validation not run: Production migration history/duplicate Draft query, backup and restore rehearsal, production DDL, Fly deploy and live API health/log verification.

## Exceptions and learning
Mistakes observed: none
Waste: One invalid plan area; one 180-second parallel-build timeout; one malformed MSBuild worker switch; default local test credentials rejected; recovered with an isolated temporary PostgreSQL 18 instance.
Missed: No access to production history, duplicate Draft count, or backup recoverability.
Follow-up: Database owner/operator: run the documented read-only production preflight and verify backup/restore; any production migration and deploy require a separate explicit approval.
Residual risk: Production migration history, duplicate Draft state, backup recovery and live API readiness remain unverified.
Documentation impact: This follow-up changed only its v2 evidence file; the prior user-assigned run owns the staged migration/runbook docs and registry updates.
Cross-repo impact: none

## Delivery
State: Needs validation
Branch/PR: codex/production-schema-reconciliation; origin/main delivery target
Commit SHA: self
Completion %: 79

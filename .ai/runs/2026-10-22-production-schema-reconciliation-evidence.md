# User-assigned production schema reconciliation Evidence

Evidence format: v2
Prompt ID: user-assigned (no separate queue ID)
Queue: user-assigned
Agent/tool: Codex, Fly CLI, EF Core tooling
Model provider: OpenAI
Model name/id: unknown-not-exposed
Client/IDE: Codex desktop
Run mode: audit
Token budget: high
Started at UTC: 2026-10-22 (exact start not captured)
Completed at UTC: 2026-10-22T15:34:34Z
Elapsed time: more than 20 minutes; exact start not captured
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-CI-001, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: Record exact proof and leave completion below Done while provider-backed migration and live history checks are unavailable.
Owner/hypothesis: DatabaseSchemaVersionGuard and docs/database-migrations.md own schema readiness; the reported mismatch was code/history drift. Current history query is the falsifier, but direct production DB access was unavailable.
Files inspected: 30
Files changed: 8
Searches: 13
Validation runs: 13
Failed retries: 1

## Outcome
- Confirmed the current API migration chain ends at `20260923145307_AddDesignTokenDraftUniqueness`; the two migrations after the reported baseline are additive except for the unique Draft index.
- Generated and statically reviewed the PostgreSQL script; updated migration and deployment guidance and documented the conditional production runbook.
- Fly v136 completed and the machine exited cleanly after a requested stop on Oct 20, but production migration rows and Draft count remain unverified; no production migration, backup, restore or deploy was run.

## Changed paths
- `scripts/db/generated/api-20260908130000-to-20260923145307-idempotent.sql`
- `docs/database-migrations.md`
- `docs/DOCS_MANIFEST.json`
- `docs/DOCS_INDEX.md`
- `docs/DOCS_REGISTRY.md`
- `APPLY_MIGRATION_FLYIO.md`
- `FLY_DEPLOYMENT_GUIDE.md`
- `.ai/runs/2026-10-22-production-schema-reconciliation-evidence.md`

## Validation
Validation run: `scripts/db/generate-prod-script.ps1` — EF build and script generation passed; static SQL assertions passed; `dotnet test ... --filter "FullyQualifiedName~DesignTokenDraftVersionTests|FullyQualifiedName~DatabaseSchemaVersionGuardTests"` — 26/26; docs health — 28 documents, 0 failures; agent-system validation passed; docs-health unit tests — 7/7; `git diff --check` passed.
Validation not run: PostgreSQL migration/repeatability/startup/data-integrity rehearsal — local PostgreSQL did not answer Npgsql; no Neon rehearsal branch or production DB credential was available.

## Exceptions and learning
Mistakes observed: none
Waste: one static-check command quoting error, corrected on retry
Missed: live production history, unique-index conflict count, Neon backup retention/restore test and PostgreSQL execution remain unverified
Follow-up: database owner/operator must provide a reachable disposable PostgreSQL or Neon branch and perform the read-only production preflight before deciding whether any approved migration is needed
Residual risk: The original failure's historical cause is known, but the current production migration history and duplicate-Draft state are not confirmed.
Documentation impact: updated docs/database-migrations.md, docs/DOCS_MANIFEST.json, docs/DOCS_INDEX.md, docs/DOCS_REGISTRY.md, APPLY_MIGRATION_FLYIO.md and FLY_DEPLOYMENT_GUIDE.md
Cross-repo impact: no

## Delivery
State: Needs validation
Branch/PR: none; local working tree
Commit SHA: self
Completion %: 79

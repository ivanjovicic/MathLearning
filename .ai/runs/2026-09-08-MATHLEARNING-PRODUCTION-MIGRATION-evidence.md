# MATHLEARNING-PRODUCTION-MIGRATION Evidence

Evidence format: v2
Prompt ID: user-assigned-production-migration-20260908
Queue: user-assigned
Agent/tool: Codex + PowerShell/psql/dotnet/Fly CLI
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: investigation
Token budget: high
Started at UTC: 2026-09-08T15:00:00Z
Completed at UTC: 2026-09-08T16:00:00Z
Elapsed time: approximately 60 minutes
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-CI-001, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: read-only audit and verified recovery point preceded the migration write; no secrets were committed or echoed.
Owner/hypothesis: API migration chain and DatabaseSchemaVersionGuard; production is April-aligned with one Admin legacy history row and can safely reach July target after data preflight.
Files inspected: 33
Files changed: 4
Searches: 4
Validation runs: 8
Failed retries: 3

## Outcome
- Production Neon schema migrated successfully through `20260728112337_AddPracticeSessionReplayState`.
- 59 history rows now exist: 58 API migrations plus the single allowed Admin legacy row; pending target range is 0.
- No duplicate, orphan, empty-key, or FK preflight/postflight problems were found.
- Explicit cosmetic catalog import completed; `/api/health/ready` and `/api/health/db` now return HTTP 200.

## Changed paths
- `src/MathLearning.Api/Services/DatabaseSchemaVersionGuard.cs`
- `tests/MathLearning.Tests/Services/DatabaseSchemaVersionGuardTests.cs`
- `docs/database-migrations.md`
- `.ai/runs/2026-09-08-MATHLEARNING-PRODUCTION-MIGRATION-evidence.md`

## Validation
Validation run: focused guard tests 22/22; full suite 1167 passed, 12 failed in unrelated existing tests; documentation health 25/25; migration script generated and executed with `ON_ERROR_STOP=1`; post-migration psql checks passed; cosmetic import completed; public health/readiness endpoints returned HTTP 200.
Validation not run: Fly CLI status/log command - Fly CLI has no token in this process; public health responses verified the running Machine.

## Exceptions and learning
Mistakes observed: BACKEND-MISTAKE-AUTH-001 repeated; prevention=never reuse exposed tokens and never weaken TLS verification.
Waste: none beyond bounded connection troubleshooting.
Missed: direct Fly CLI status/log verification remains unavailable in this process.
Follow-up: optionally authenticate `flyctl` for machine/log inspection; separately persist API Data Protection keys if restart-stable protected data is required.
Residual risk: full test suite has 12 unrelated failures and the production Machine is still on the pre-deploy image.
Documentation impact: updated `docs/database-migrations.md`.
Cross-repo impact: no.

## Delivery
State: Needs validation
Branch/PR: `agent/BACKEND-SEASON-DAILY-RUN-PROVENANCE-001` pushed to origin
Commit SHA: self
Completion %: 79

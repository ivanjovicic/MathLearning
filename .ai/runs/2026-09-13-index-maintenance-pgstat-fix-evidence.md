# Index maintenance pg_stat_user_indexes fix

Evidence format: v2
Prompt ID: direct-index-maintenance-pgstat-fix-20260913
Queue: user-assigned
Agent/tool: Codex desktop + PowerShell
Model provider: OpenAI
Model name/id: GPT-5
Client/IDE: Codex desktop
Run mode: known-fix
Token budget: medium
Started at UTC: not recorded - original implementation run did not capture the timestamp.
Completed at UTC: 2026-09-13T06:59:28Z - evidence validator repair completed.
Elapsed time: not recorded for the original run; validator repair was under 1m.
Relevant prior mistakes read: BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: kept the SQL correction narrow, added a focused contract test, and did not infer live PostgreSQL proof from source tests.
Owner/hypothesis: Index maintenance SQL contract; PostgreSQL catalog aliases must use `relname` and `indexrelname`.
Files inspected: 8
Files changed: 3
Searches: 1
Validation runs: 5
Failed retries: 1
Mistakes observed: BACKEND-MISTAKE-VALIDATION-001
Waste: one focused-test count correction during test authoring
Missed: live PostgreSQL fixture execution
Follow-up: repository owner; run the maintenance SQL against a non-production PostgreSQL fixture.
Residual risk: PostgreSQL runtime execution remains unverified; unrelated repository test failures still block required CI validation.
Cross-repo impact: none
State: Needs validation
Branch/PR: codex/fix-index-maintenance-pgstat-20260913 -> PR #28
Completion %: 79
Commit SHA: 1ceb41e207b144cb354815c81a669290c5c775c7
Outcome: corrected both production queries that selected nonexistent `tablename`/`indexname` columns from `pg_stat_user_indexes`; bloat metric intentionally unchanged.
Owner/source of truth: `src/MathLearning.Infrastructure/Maintenance/IndexMaintenanceService.cs`
Assumption: PostgreSQL `pg_stat_user_indexes` exposes `relname` and `indexrelname` as supplied by the user diagnosis.
Expected changed files: service, focused regression test, this evidence log.
Focused proof: source contract test plus Release solution build.
Stop/handoff trigger: live PostgreSQL fixture proof requires an authenticated/non-production database connection.
Documentation impact: none - the documented bloat metric remains unchanged and this is a source-level SQL identifier correction.
Delivery target: PR from `codex/fix-index-maintenance-pgstat-20260913`.

## Files changed
- `src/MathLearning.Infrastructure/Maintenance/IndexMaintenanceService.cs`
- `tests/MathLearning.Tests/Infrastructure/IndexMaintenanceSqlContractTests.cs`
- `.ai/runs/2026-09-13-index-maintenance-pgstat-fix-evidence.md`

## Validation
- `dotnet restore tests/MathLearning.Tests/MathLearning.Tests.csproj` -> pass.
- Focused SQL contract test initially exposed its own incorrect count for the already-correct third query; test corrected, then `dotnet test ... --filter FullyQualifiedName~IndexMaintenanceSqlContractTests --no-restore` -> pass, 1 test.
- `dotnet restore MathLearning.slnx` -> pass.
- `dotnet build MathLearning.slnx -c Release --no-restore` -> pass, 0 errors.
- `git diff --check` -> pass.
- Live PostgreSQL execution -> not run - no database credentials/fixture supplied.

## What was done
- `CheckIndexHealthAsync` now selects `i.relname AS tablename` and `i.indexrelname AS indexname`.
- `DetectBloatedIndexesAsync` now selects `relname AS tablename` and `indexrelname AS indexname`.
- `DetectUnusedIndexesAsync`, `CheckIndexes` and `ApplyMigration` were inspected and left unchanged because their existing columns are valid for their respective views.
- Added a regression contract test that rejects the invalid service references and requires both corrected aliases.

## What was not done
- No change to `pg_relation_size` bloat calculation, thresholds, rebuild behavior or API shape.
- No production deployment or live database verification.

## Risks and residuals
- Build reports existing package advisories for OpenTelemetry/System.Text.Json and nullable warnings in TranslationJob; unrelated to this fix.
- The bloat metric remains covered by `BACKEND-TEST-048-index-bloat-validation.md` and still needs PostgreSQL fixture evidence.

## Closure verdict
- Done allowed: no - PR delivery and live PostgreSQL runtime proof remain pending.
- Completion %: 79

## Commit SHA
1ceb41e207b144cb354815c81a669290c5c775c7

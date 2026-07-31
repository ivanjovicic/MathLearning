# BACKEND-API-DB-015 Evidence

Evidence format: v2
Prompt ID: BACKEND-API-DB-015
Queue: docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md
Agent/tool: cursor-composer
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: cursor
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-07-31T17:37:00Z
Completed at UTC: 2026-07-31T17:55:00Z
Elapsed time: 18m
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-IDEM-001, BACKEND-MISTAKE-IDEM-002, BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes: Pattern A only for economy; cosmetics deferred; prove begin-outside tombstone vs begin-inside rollback recovery
Owner/hypothesis: economy Begin commits pending outside domain tx so crash leaves permanent transaction_in_progress
Files inspected: 10
Files changed: 9
Searches: 3
Validation runs: 1
Failed retries: 0
Budget note: medium path budget exceeded (9>6) for season queue integrity repair plus economy Pattern A; completion capped at 79

## Outcome
- Economy settlement routes claim via `BeginClaimInTransactionAsync` (ambient DB tx before ledger insert).
- Abandoned claim after begin rolls back pending; retry settles once with single completed row.
- Season queue conflict markers cleared; TRACK/DAILY-RUN/XP marked Done from main-verified evidence.

## Changed paths
- src/MathLearning.Api/Endpoints/EconomyEndpointHelpers.cs
- src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs
- tests/MathLearning.Tests/Idempotency/RelationalIdempotencyTransactionTests.cs
- docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md
- docs/prompt_queues/backend_season_authority_residuals_2026_07_31.md
- .ai/runs/2026-07-31-BACKEND-API-DB-015-evidence.md
- .ai/runs/2026-07-31-BACKEND-SEASON-TRACK-AUTHORITY-001-evidence.md
- .ai/runs/2026-07-31-BACKEND-SEASON-XP-SETTLEMENT-001-evidence.md
- .ai/runs/2026-07-31-BACKEND-SEASON-DAILY-RUN-PROVENANCE-001-evidence.md

## Validation
Validation run: `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~RelationalIdempotencyTransactionTests|FullyQualifiedName~EconomyTransaction|FullyQualifiedName~SeasonDailyRun|FullyQualifiedName~SeasonMilestone` → Passed 37 / 0
Validation not run: cosmetics pending Pattern A; PostgreSQL takeover race matrix

## Exceptions and learning
Mistakes observed: none
Waste: season queue had unresolved conflict markers after Daily Run merge; repaired in same delivery
Missed: none
Follow-up: cosmetics_idempotency_ledger Pattern A slice; PG concurrency matrix under TEST-032/033
Residual risk: cosmetics begin-outside tombstone remains; push/PR/main open
Documentation impact: season queue integrity restored; API-DB-015 status updated
Cross-repo impact: no - response contracts unchanged

## Delivery
State: Needs merge
Branch/PR: cursor/backend-api-db-015-pending-recovery-fa87
Commit SHA: self
Completion %: 79

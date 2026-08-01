# BACKEND-API-DB-015 Evidence (cosmetics Pattern A slice)

Evidence format: v2
Prompt ID: BACKEND-API-DB-015
Queue: docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md
Agent/tool: cursor-composer
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: cursor
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-08-01T06:22:00Z
Completed at UTC: 2026-08-01T06:40:00Z
Elapsed time: 18m
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-IDEM-001, BACKEND-MISTAKE-IDEM-002, BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes: cosmetics-only Pattern A; economy owned by PR #16; prove tombstone vs rollback recovery
Owner/hypothesis: cosmetics Begin commits pending outside domain tx so crash leaves permanent transaction_in_progress
Files inspected: 8
Files changed: 5
Searches: 3
Validation runs: 1
Failed retries: 1

## Outcome
- Cosmetics item claim, fragment grant and shop purchase claim via `BeginClaimInTransactionAsync`.
- Abandoned cosmetics claim rolls back pending; retry settles once.
- Economy Pattern A remains on PR #16; this slice completes the cosmetics half of 015.

## Changed paths
- src/MathLearning.Api/Endpoints/CosmeticsEndpointHelpers.cs
- src/MathLearning.Api/Endpoints/CosmeticsEndpoints.cs
- src/MathLearning.Api/Endpoints/AvatarEndpoints.cs
- tests/MathLearning.Tests/Idempotency/RelationalIdempotencyTransactionTests.cs
- docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md
- .ai/runs/2026-08-01-BACKEND-API-DB-015-cosmetics-evidence.md

## Validation
Validation run: `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~RelationalIdempotencyTransactionTests|FullyQualifiedName~CosmeticsIdempotencyServiceTests` → Passed 18 / 0
Validation not run: PostgreSQL takeover race matrix; merge with economy PR #16

## Exceptions and learning
Mistakes observed: none
Waste: CosmeticsEndpoints double-newline collapse briefly dropped fragment target resolve; restored with FailAsync inside tx
Missed: none
Follow-up: merge with economy Pattern A PR #16; PG concurrency matrix
Residual risk: economy begin-outside still on main until PR #16 merges; push/PR/main open
Documentation impact: queue status for 015 cosmetics slice
Cross-repo impact: no

## Delivery
State: Needs merge
Branch/PR: cursor/backend-api-db-015-cosmetics-pending-fa87
Commit SHA: self
Completion %: 79

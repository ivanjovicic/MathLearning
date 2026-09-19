# BACKEND-API-DB-015 Evidence

Evidence format: v2
Prompt ID: BACKEND-API-DB-015
Queue: formal
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: high
Started at UTC: 2026-09-19T10:34:01Z
Completed at UTC: 2026-09-19T11:05:00Z
Elapsed time: 31m
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-IDEM-001, BACKEND-MISTAKE-IDEM-002, BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-IDEM-001; apply BACKEND-MISTAKE-IDEM-002; apply BACKEND-MISTAKE-XREPO-001
Owner/hypothesis: Pending economy/cosmetics rows need durable ownership so one stale request can be recovered exactly once; falsifier would be an active lease being taken over or an old owner completing after takeover.
Files inspected: 12
Files changed: 14
Searches: 6
Validation runs: 4
Failed retries: 2

## Outcome
- Implemented five-minute durable leases for economy and cosmetics idempotency rows, atomic stale takeover, ownership checks, attempt counters, indexes and API migration.
- Added red-first takeover/old-owner regression tests; focused suite is green.
- PostgreSQL provider/concurrency matrix is not proven locally because configured PostgreSQL at `localhost:5433` is unavailable.

## Changed paths
- docs/mobile_contract_idempotency_handoff.md
- docs/prompt_queues/backend_test_coverage.md
- src/MathLearning.Domain/Entities/EconomyEntities.cs
- src/MathLearning.Domain/Entities/EconomyTransaction.cs
- src/MathLearning.Infrastructure/Migrations/Api/20260919104120_AddIdempotencyLeases.cs
- src/MathLearning.Infrastructure/Migrations/Api/20260919104120_AddIdempotencyLeases.Designer.cs
- src/MathLearning.Infrastructure/Migrations/Api/ApiDbContextModelSnapshot.cs
- src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs
- src/MathLearning.Infrastructure/Persistance/Configurations/EconomyTransactionConfiguration.cs
- src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticsIdempotencyService.cs
- src/MathLearning.Infrastructure/Services/EconomyTransactionService.cs
- tests/MathLearning.Tests/Services/CosmeticsIdempotencyServiceTests.cs
- tests/MathLearning.Tests/Services/EconomyTransactionServiceTests.cs
- .ai/runs/2026-09-19-BACKEND-API-DB-015-evidence.md

## Validation
Validation run: red pre-change 18/20 (the two new takeover tests failed as expected); green post-change focused `dotnet test ... --filter "FullyQualifiedName~EconomyTransactionServiceTests|FullyQualifiedName~CosmeticsIdempotencyServiceTests"` 20/20; `dotnet ef migrations add AddIdempotencyLeases` completed; `git diff --check` pass.
Validation not run: PostgreSQL failure/concurrency matrix, Docker/Neon/Fly proof, and full suite.

## Exceptions and learning
Mistakes observed: BACKEND-MISTAKE-VALIDATION-001 — InMemory does not translate ExecuteUpdate, so tests use a clearly bounded provider fallback; relational path remains conditional database update.
Waste: First local attempt used a compound nullable predicate that InMemory could not translate; split the relational predicate into null-lease and expired-lease updates.
Missed: No local PostgreSQL race proof was available.
Follow-up: Run the required PostgreSQL failure/concurrency matrix and deployed schema verification under the existing validation owner.
Residual risk: PostgreSQL transaction/replica takeover behavior and production migration application remain unverified in this run.
Documentation impact: Updated the durable mobile idempotency handoff and queue status; generated migration/snapshot synchronized.
Cross-repo impact: No Flutter changes; mobile contract remains compatible and now documents recovery semantics.

## Delivery
State: Needs validation
Branch/PR: `agent/BACKEND-API-DB-015` delivered to `origin/main`; PR metadata not available in this environment
Commit SHA: self
Completion %: 79

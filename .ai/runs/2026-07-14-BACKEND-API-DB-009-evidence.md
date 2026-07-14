# BACKEND-API-DB-009 Evidence

Prompt ID: BACKEND-API-DB-009
Queue: docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md
Agent/tool: Codex
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: reward/inventory trust-boundary repair with PostgreSQL transaction proof
Token budget: unknown-not-exposed
Actual context: Enforce server-authoritative cosmetic item and fragment entitlement flow without Admin project work.
Started from queue status: Prompt-ready
Local collision check: Existing uncommitted BACKEND-TEST-022 files were already present; left intact and isolated this prompt to cosmetics/mobile backend paths.
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-QUEUE-001, BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes: Run log created before edits, canonical queue owner rechecked before claiming the ID, and docs/contracts/queue rows were updated in the same run as runtime changes.
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Files inspected

- docs/prompt_queues/backend_test_coverage.md
- docs/prompt_queues/backend_api_db_residuals_2026_07_11.md
- docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md
- src/MathLearning.Api/Endpoints/CosmeticsEndpoints.cs
- src/MathLearning.Api/Endpoints/AvatarEndpoints.cs
- src/MathLearning.Api/Endpoints/DailyRunCosmeticsSettlement.cs
- src/MathLearning.Api/Endpoints/CosmeticsEndpointHelpers.cs
- src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Public.cs
- src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Rewards.cs
- src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticsFragmentService.cs
- src/MathLearning.Domain/Entities/CosmeticItem.cs
- tests/MathLearning.Tests/Contracts/MobileCosmeticsApiIntegrationTests.cs
- tests/MathLearning.Tests/Contracts/MobileCosmeticsContractIntegrationTests.cs
- tests/MathLearning.Tests/Contracts/MobileEconomyContractIntegrationTests.cs
- tests/MathLearning.Tests/Endpoints/EconomySettlementEndpointsIntegrationTests.cs
- tests/MathLearning.Tests/Endpoints/DailyRunFragmentGrantTrustBoundaryTests.cs
- tests/MathLearning.Tests/Endpoints/IdempotencyObservabilityEndpointsTests.cs
- tests/MathLearning.Tests/Idempotency/CosmeticsMutationResponseTests.cs

## Files changed

- docs/API_ENDPOINT_INVENTORY.md
- docs/backend_contract_gap_report.md
- docs/mobile_api_contract.md
- docs/mobile_economy_api_contract.md
- docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md
- docs/prompt_queues/backend_test_coverage.md
- src/MathLearning.Api/Endpoints/AvatarEndpoints.cs
- src/MathLearning.Api/Endpoints/CosmeticsEndpoints.cs
- src/MathLearning.Application/DTOs/Cosmetics/CosmeticDtos.cs
- src/MathLearning.Application/Services/CosmeticServices.cs
- src/MathLearning.Domain/Entities/CosmeticItem.cs
- src/MathLearning.Infrastructure/DependencyInjection.cs
- src/MathLearning.Infrastructure/Migrations/Api/20260714142315_AddCosmeticEntitlementsAuthority.cs
- src/MathLearning.Infrastructure/Migrations/Api/20260714142315_AddCosmeticEntitlementsAuthority.Designer.cs
- src/MathLearning.Infrastructure/Migrations/Api/ApiDbContextModelSnapshot.cs
- src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs
- src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticEntitlementService.cs
- src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Public.cs
- src/MathLearning.Infrastructure/Services/Idempotency/IdempotencyObservabilityService.cs
- tests/MathLearning.Tests/Contracts/MobileCosmeticsApiIntegrationTests.cs
- tests/MathLearning.Tests/Contracts/MobileCosmeticsContractIntegrationTests.cs
- tests/MathLearning.Tests/Contracts/MobileEconomyContractIntegrationTests.cs
- tests/MathLearning.Tests/Contracts/MobileEconomyContractPayloads.cs
- tests/MathLearning.Tests/Endpoints/CosmeticsPurchaseEndpointTests.cs
- tests/MathLearning.Tests/Endpoints/EconomySettlementEndpointsIntegrationTests.cs
- tests/MathLearning.Tests/Endpoints/IdempotencyObservabilityEndpointsTests.cs
- tests/MathLearning.Tests/Helpers/CosmeticEntitlementTestSeeder.cs
- tests/MathLearning.Tests/Idempotency/CosmeticsMutationResponseTests.cs

## Commands run

- rg -n "BACKEND-API-DB-009|BACKEND-API-DB-010|BACKEND-API-DB-011|BACKEND-API-DB-012|BACKEND-API-DB-013|BACKEND-API-DB-014|Prompt-ready|Done" docs/prompt_queues/backend_api_db_* docs/prompt_queues/backend_test_coverage.md
- dotnet build src/MathLearning.Api/MathLearning.Api.csproj -nologo
- dotnet build tests/MathLearning.Tests/MathLearning.Tests.csproj -nologo
- dotnet ef migrations add AddCosmeticEntitlementsAuthority --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext
- dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext
- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~MobileCosmeticsApiIntegrationTests|FullyQualifiedName~MobileCosmeticsContractIntegrationTests|FullyQualifiedName~MobileEconomyContractIntegrationTests|FullyQualifiedName~CosmeticsMutationResponseTests|FullyQualifiedName~EconomySettlementEndpointsIntegrationTests|FullyQualifiedName~IdempotencyObservabilityEndpointsTests|FullyQualifiedName~DailyRunFragmentGrantTrustBoundaryTests|FullyQualifiedName~CosmeticsPurchaseEndpointTests" --no-build -nologo

## What was done

- Added a new `CosmeticEntitlement` domain/model/migration path and registered `ICosmeticEntitlementService`.
- Changed `POST /api/cosmetics/items/{itemKey}/claim` to require a server-issued entitlement and derive item/source provenance from trusted storage.
- Changed non-Daily-Run `POST /api/cosmetics/fragments/grant` to require a server-issued entitlement while preserving the existing Daily Run server-derived settlement path.
- Hardened `POST /api/cosmetics/purchase` with cosmetics-ledger idempotency plus server-owned release/hidden/default/retirement/price checks.
- Added focused purchase endpoint tests and entitlement seed helpers.
- Updated mobile/backend contract docs and queue status.

## What was missed

- `scripts/db/validate-schema.ps1` was not rerun successfully because the local PostgreSQL target on `localhost:5433` was unavailable during this run.

## Validation run

- `dotnet build src/MathLearning.Api/MathLearning.Api.csproj -nologo`
- `dotnet build tests/MathLearning.Tests/MathLearning.Tests.csproj -nologo`
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~MobileCosmeticsApiIntegrationTests|FullyQualifiedName~MobileCosmeticsContractIntegrationTests|FullyQualifiedName~MobileEconomyContractIntegrationTests|FullyQualifiedName~CosmeticsMutationResponseTests|FullyQualifiedName~EconomySettlementEndpointsIntegrationTests|FullyQualifiedName~IdempotencyObservabilityEndpointsTests|FullyQualifiedName~DailyRunFragmentGrantTrustBoundaryTests|FullyQualifiedName~CosmeticsPurchaseEndpointTests" --no-build -nologo`
- `dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext`

## Validation not run

- `powershell -ExecutionPolicy Bypass -File scripts/db/validate-schema.ps1` because local PostgreSQL was unavailable.

## Waste categories

- none

## Mistakes observed

- none

## Where time/context was wasted

- none

## Why waste happened

- none

## What the next agent should avoid

- Do not treat client `source`/`sourceType`/`sourceEvent`/fragment quantity as authority for cosmetics grants.
- Do not mark this queue row fully Done until PostgreSQL schema validation runs against a reachable local database.

## Docs/rules updated to prevent repeat

- docs/mobile_economy_api_contract.md
- docs/mobile_api_contract.md
- docs/API_ENDPOINT_INVENTORY.md
- docs/backend_contract_gap_report.md

## Queue updated

- `docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md` -> `Runtime-fixed / Needs schema validation`
- `docs/prompt_queues/backend_test_coverage.md` -> `Runtime-fixed / Needs schema validation`

## New optimized prompt added

- none

## Follow-up prompt

- Re-run schema validation and PostgreSQL-backed mutation proof for `BACKEND-API-DB-009` once local DB is reachable; if green, promote queue row from `Runtime-fixed / Needs schema validation` to `Done`.

## Completion %

- 90

## Residual risk

- PostgreSQL-specific schema/runtime validator was not executed because local DB connectivity was unavailable.
- Existing unrelated `BACKEND-TEST-022` dirty worktree changes remain uncommitted and were intentionally not mixed into this prompt.

## Commit SHA

- none

# BACKEND-API-DB-019 Evidence

Prompt ID: BACKEND-API-DB-019
Queue: docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md
Agent/tool: Codex / functions.exec_command
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: implementation + migration/bootstrap/readiness validation
Token budget: medium
Actual context: versioned cosmetics catalog ownership, explicit operator import and readiness enforcement
Started from queue status: Prompt-ready in pass 3
Local collision check: no existing 2026-07-16 BACKEND-API-DB-019 run log found
Relevant prior mistakes read:
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001
- BACKEND-MISTAKE-AUTH-001
How this run avoids prior mistakes:
- keep catalog ownership explicit, verify startup/readiness with provider-backed tests, and document the operator import boundary instead of silently mutating data.
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Files inspected

- `AGENTS.md`
- `docs/AGENT_RUN_LOG_ENFORCEMENT.md`
- `.ai/RUN_LOG_TEMPLATE.md`
- `.ai/runs/README.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`
- `docs/prompt_queues/backend_api_db_pass3/BACKEND-API-DB-019.md`
- `docs/BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_16_PASS3.md`
- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Endpoints/HealthEndpoints.cs`
- `src/MathLearning.Api/Startup/CosmeticStartupSeeder.cs`
- `src/MathLearning.Application/Services/CosmeticServices.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Helpers.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Public.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Mobile.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Rewards.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticCatalogManifest.cs`
- `src/MathLearning.Domain/Entities/CosmeticCatalogRevision.cs`
- `src/MathLearning.Application/DTOs/Cosmetics/CosmeticCatalogOpsDtos.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/20260716154600_AddCosmeticCatalogRevisions.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/20260716154600_AddCosmeticCatalogRevisions.Designer.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/ApiDbContextModelSnapshot.cs`
- `tests/MathLearning.Tests/Services/CosmeticCatalogImportTests.cs`
- `tests/MathLearning.Tests/Endpoints/CosmeticCatalogHealthEndpointTests.cs`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/ARCHITECTURE_OVERVIEW.md`
- `docs/BACKEND_COLD_START_BUDGET.md`
- `docs/mobile_api_contract.md`

## Files changed

- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Endpoints/HealthEndpoints.cs`
- `src/MathLearning.Api/Startup/CosmeticStartupSeeder.cs` deleted
- `src/MathLearning.Application/Services/CosmeticServices.cs`
- `src/MathLearning.Application/DTOs/Cosmetics/CosmeticCatalogOpsDtos.cs`
- `src/MathLearning.Domain/Entities/CosmeticCatalogRevision.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticCatalogManifest.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Helpers.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Public.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Mobile.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Rewards.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/20260716154600_AddCosmeticCatalogRevisions.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/20260716154600_AddCosmeticCatalogRevisions.Designer.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/ApiDbContextModelSnapshot.cs`
- `tests/MathLearning.Tests/Services/CosmeticCatalogImportTests.cs`
- `tests/MathLearning.Tests/Endpoints/CosmeticCatalogHealthEndpointTests.cs`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/ARCHITECTURE_OVERVIEW.md`
- `docs/BACKEND_COLD_START_BUDGET.md`
- `docs/mobile_api_contract.md`
- `.ai/runs/2026-07-16-BACKEND-API-DB-019-evidence.md`
- `docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`

## Commands run

- `git status --short --branch`
- `rg -n "CosmeticStartupSeeder|apply-cosmetic-catalog|catalog-20260716-019|cosmetic_catalog_revisions|SchemaNotReady|CatalogNotReady" src tests docs .ai -S`
- `Get-Content .ai/RUN_LOG_TEMPLATE.md`
- `Get-Content .ai\\runs\\README.md`
- `Get-Content .ai\\runs\\2026-07-16-BACKEND-API-DB-019-evidence.md -ErrorAction SilentlyContinue`
- `rg -n "BACKEND-MISTAKE-(EVIDENCE|VALIDATION|XREPO|AUTH)-001" docs/ai/learning/MISTAKE_LEDGER.md`
- `git diff -- src/MathLearning.Api/Startup/CosmeticStartupSeeder.cs src/MathLearning.Api/Program.cs src/MathLearning.Api/Endpoints/HealthEndpoints.cs --`
- `Get-Content docs/prompt_queues/backend_api_db_pass3/BACKEND-API-DB-019.md`
- `Get-Content docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md | Select-Object -First 120`
- `Get-Content docs/BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_16_PASS3.md`
- `dotnet build MathLearning.slnx -c Release`
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~CosmeticCatalog"`
- `dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext`

## What was done

- Replaced silent cosmetics startup mutation with an explicit `--apply-cosmetic-catalog` import path.
- Added versioned catalog revision tracking, manifest checksuming and readiness checks.
- Wired health/readiness to fail when the cosmetic catalog revision/defaults/fragments are not valid.
- Added PostgreSQL-backed tests for import, no-op reapply, explicit deploy-managed updates, concurrency and readiness behavior.
- Updated docs so endpoint inventory, architecture, cold-start budget and mobile contract all agree on the new catalog ownership model.
- Deleted the old startup seeder so normal startup no longer mutates catalog rows.

## What was missed

- No GitHub Actions evidence was checked in this session.

## Validation run

- `dotnet build MathLearning.slnx -c Release` passed.
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~CosmeticCatalog"` passed with 5 tests.
- `dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext` returned `No changes have been made to the model since the last migration.`

## Validation not run

- not run - no GitHub Actions evidence found via connector

## Waste categories

- SQL quoting mismatch in the generated manifest import string.
- Import concurrency race until the revision existence re-check was moved inside the advisory lock/transaction boundary.
- Health test needed a repo-specific relaxation because the in-memory factory failed schema readiness before catalog readiness.

## Mistakes observed

- none

## Where time/context was wasted

- Rebuilding the manifest SQL after the first quoting failure.
- Reworking the import flow once concurrent application attempts exposed a race.

## Why waste happened

- The catalog import moved a large amount of seeded data into a new explicit manifest path, so quoting and transaction boundaries needed one validation pass to align with PostgreSQL behavior.

## What the next agent should avoid

- Reintroducing silent product-data mutation into normal API startup.
- Treating a warning-only catalog check as readiness proof.
- Skipping provider-backed proof when catalog ownership or revision rules change.

## Docs/rules updated to prevent repeat

- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/ARCHITECTURE_OVERVIEW.md`
- `docs/BACKEND_COLD_START_BUDGET.md`
- `docs/mobile_api_contract.md`
- `docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`

## Queue updated

- `BACKEND-API-DB-019` marked `Done` with run log `.ai/runs/2026-07-16-BACKEND-API-DB-019-evidence.md`.

## New optimized prompt added

- none

## Follow-up prompt

- none

## Completion %

- 100%

## Residual risk

- Operators now need to run `--apply-cosmetic-catalog` when a new catalog revision is deployed; if they skip it, readiness will remain blocked for cosmetics-dependent flows instead of silently mutating data.

## Commit SHA

- b515950

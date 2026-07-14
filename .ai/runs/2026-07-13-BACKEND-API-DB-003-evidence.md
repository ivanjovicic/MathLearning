# BACKEND-API-DB-003 Evidence

Prompt ID: BACKEND-API-DB-003
Queue: docs/prompt_queues/backend_api_db_residuals_2026_07_11.md
Agent/tool: Codex desktop
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: Codex desktop
Run mode: implementation, contract/security fix
Token budget: unknown-not-exposed
Elapsed time: unknown-not-recorded
Phase time breakdown: inventory 00:00:00; implementation 00:00:00; validation 00:00:00
Started from queue status: Prompt-ready
Local collision check: repo already has unrelated working-tree changes; no new collision introduced yet
Relevant prior mistakes read:
- BACKEND-MISTAKE-XREPO-001
- BACKEND-MISTAKE-IDEM-002
- BACKEND-MISTAKE-VALIDATION-002
How this run avoids prior mistakes:
- inspect the canonical progress sync path and reward projection boundaries before changing behavior
- prove the server refuses client-authoritative completion/day claims without evidence
- keep reward projection idempotent and replay-safe while preserving legitimate offline sync semantics

## Files inspected

- `src/MathLearning.Api/Endpoints/ProgressEndpoints.cs`
- `src/MathLearning.Api/Endpoints/ProgressEndpointHelpers.cs`
- `src/MathLearning.Application/DTOs/Progress/ProgressSyncDtos.cs`
- `src/MathLearning.Domain/Entities/UserDailyStat.cs`
- `src/MathLearning.Domain/Entities/SyncEventLog.cs`
- `src/MathLearning.Domain/Entities/SyncDevice.cs`
- `src/MathLearning.Domain/Entities/PracticeSessionModels.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Rewards.cs`
- `src/MathLearning.Infrastructure/Services/Sync/SyncOptions.cs`
- `src/MathLearning.Infrastructure/Services/Sync/SyncService.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/ApiDbContextModelSnapshot.cs`
- `docs/prompt_queues/backend_api_db_residuals_2026_07_11.md`
- `docs/API_ENDPOINT_INVENTORY.md`

## Files changed

- `src/MathLearning.Api/Endpoints/ProgressEndpoints.cs`
- `src/MathLearning.Api/Endpoints/ProgressEndpointHelpers.cs`
- `src/MathLearning.Application/DTOs/Progress/ProgressSyncDtos.cs`
- `src/MathLearning.Infrastructure/Services/Sync/SyncOptions.cs`
- `tests/MathLearning.Tests/Endpoints/ProgressSyncIntegrationTests.cs`
- `tests/MathLearning.Tests/Idempotency/MutationUserScopeIntegrationTests.cs`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11.md`

## Commands run

- `rg -n "progress/sync|ProgressSync|ProcessProgressRewardsAsync|UserDailyStat|UserDailyStats|daily completion|completed: true|reward" src tests -g "*.cs"`
- `Get-Content src/MathLearning.Api/Endpoints/ProgressEndpoints.cs | Select-Object -First 320`
- `Get-Content src/MathLearning.Domain/Entities/UserDailyStat.cs; Write-Host '---'; Get-Content src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticRewardService.cs | Select-Object -First 260`
- `Get-Content src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Rewards.cs | Select-Object -First 220`
- `Get-Content src/MathLearning.Infrastructure/Services/Sync/SyncService.cs | Select-Object -First 220`
- `Get-Content tests/MathLearning.Tests/Services/DailyStreakTests.cs | Select-Object -First 240`
- `Get-Content tests/MathLearning.Tests/Models/UserDailyStatModelTests.cs | Select-Object -First 120`
- `Get-Content src/MathLearning.Application/DTOs/Sync/SyncDtos.cs | Select-Object -First 220`
- `Get-Content src/MathLearning.Api/Endpoints/SyncEndpoints.cs | Select-Object -First 220`
- `Get-Content tests/MathLearning.Tests/Idempotency/MutationUserScopeIntegrationTests.cs | Select-Object -First 220`
- `Get-Content tests/MathLearning.Tests/Services/SyncServiceTests.cs | Select-Object -First 260`
- `Get-Content docs/prompt_queues/backend_api_db_residuals_2026_07_11.md | Select-Object -Skip 200 -First 120`
- `Get-Content docs/API_ENDPOINT_INVENTORY.md | Select-Object -Skip 116 -First 24`
- `dotnet build tests/MathLearning.Tests/MathLearning.Tests.csproj -c Debug -v minimal`
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Debug --filter "FullyQualifiedName~ProgressSyncIntegrationTests|FullyQualifiedName~MutationUserScopeIntegrationTests"`
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~ProgressSync|FullyQualifiedName~UserDailyStat|FullyQualifiedName~CosmeticReward|FullyQualifiedName~MutationUserScope|FullyQualifiedName~Sync"`
- `dotnet build MathLearning.slnx -c Release`
- `git diff --check -- src/MathLearning.Api/Endpoints/ProgressEndpoints.cs src/MathLearning.Api/Endpoints/ProgressEndpointHelpers.cs src/MathLearning.Application/DTOs/Progress/ProgressSyncDtos.cs src/MathLearning.Infrastructure/Services/Sync/SyncOptions.cs tests/MathLearning.Tests/Endpoints/ProgressSyncIntegrationTests.cs tests/MathLearning.Tests/Idempotency/MutationUserScopeIntegrationTests.cs docs/API_ENDPOINT_INVENTORY.md docs/BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11.md`

## What was done

- Replaced `/api/progress/sync`'s raw `JsonElement` client-authoritative body with a typed `ProgressSyncRequestDto`.
- Added stable progress-sync operation identity plus canonical payload hashing through the existing idempotency ledger.
- Required a registered active device, verified settled quiz/practice evidence belongs to the authenticated user/device, bounded accepted days, and rejected legacy `completed/day` payloads with an explicit compatibility response.
- Updated `UserDailyStat` settlement to be transactionally idempotent and only trigger reward processing on the first verified completion.
- Added integration coverage for legacy rejection, valid evidence completion, replay stability, conflict detection, device-scope rejection, and date window rejection.
- Updated the progress API inventory and residual audit note to reflect the new contract.

## What was missed

- Full PostgreSQL concurrency race evidence was not added in this pass.

## Validation run

- `dotnet build tests/MathLearning.Tests/MathLearning.Tests.csproj -c Debug -v minimal`
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Debug --filter "FullyQualifiedName~ProgressSyncIntegrationTests|FullyQualifiedName~MutationUserScopeIntegrationTests"`
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~ProgressSync|FullyQualifiedName~UserDailyStat|FullyQualifiedName~CosmeticReward|FullyQualifiedName~MutationUserScope|FullyQualifiedName~Sync"`
- `dotnet build MathLearning.slnx -c Release`
- `git diff --check -- src/MathLearning.Api/Endpoints/ProgressEndpoints.cs src/MathLearning.Api/Endpoints/ProgressEndpointHelpers.cs src/MathLearning.Application/DTOs/Progress/ProgressSyncDtos.cs src/MathLearning.Infrastructure/Services/Sync/SyncOptions.cs tests/MathLearning.Tests/Endpoints/ProgressSyncIntegrationTests.cs tests/MathLearning.Tests/Idempotency/MutationUserScopeIntegrationTests.cs docs/API_ENDPOINT_INVENTORY.md docs/BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11.md`

## Validation not run

- A dedicated PostgreSQL concurrency matrix for progress settlement was not executed.

## Waste categories

- None material.

## Mistakes observed

Mistakes observed: none

## Where time/context was wasted

- One compile pass failed because `Question` does not expose `TopicId`; `GetTopicProgressAsync` was corrected back to the existing `Question -> Subtopic -> Topic` path.

## Why waste happened

- I inferred the wrong topic join from the progress refactor and had to align it with the actual domain model.

## What the next agent should avoid

- Reusing `Question.TopicId` in this codebase; progress topic grouping must join via `Subtopic`.
- Reintroducing raw `JsonElement` acceptance or client-authored `completed/day` progress claims.

## Docs/rules updated to prevent repeat

- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11.md`

## Queue updated

- `BACKEND-API-DB-003` completed in the working tree.

## New optimized prompt added

- None.

## Follow-up prompt

None

## Completion %

100%

## Residual risk

Residual risk is limited to deeper PostgreSQL race evidence beyond the current integration coverage; the endpoint contract, replay, conflict and user/device scope protections passed targeted validation.

Commit SHA: 9b01a629e7571375986d85dce8075652fc680ad8

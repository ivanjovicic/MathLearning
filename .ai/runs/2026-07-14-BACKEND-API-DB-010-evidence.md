# BACKEND-API-DB-010 Evidence

Prompt ID: BACKEND-API-DB-010
Queue: docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md
Agent/tool: Codex
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: compatibility-surface shutdown or canonical adapter repair
Token budget: unknown-not-exposed
Actual context: Remove authoritative legacy coin/hint/power-up bypasses without touching Admin project.
Started from queue status: Prompt-ready
Local collision check: Existing dirty worktree already contains BACKEND-TEST-022 and BACKEND-API-DB-009 changes; leaving them intact and isolating this prompt to legacy economy/hint/power-up compatibility paths.
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-QUEUE-001, BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes: Created evidence log before edits, re-read queue ownership before claiming the next prompt, and will update contract/docs with runtime changes.

## Files inspected

- docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md
- docs/prompt_queues/backend_test_coverage.md
- docs/ai/learning/MISTAKE_LEDGER.md
- src/MathLearning.Api/Endpoints/CoinEndpoints.cs
- src/MathLearning.Api/Endpoints/HintEndpoints.cs
- src/MathLearning.Api/Endpoints/PowerupEndpoints.cs
- src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs
- docs/BACKEND_ROUTE_COMPATIBILITY_AUDIT.md
- tests/MathLearning.Tests/Contracts/MobileMutationContractIntegrationTests.cs
- tests/MathLearning.Tests/Idempotency/EconomyOperationIdIdempotencyTests.cs

## Files changed

- docs/API_ENDPOINT_INVENTORY.md
- docs/backend_contract_gap_report.md
- docs/mobile_economy_api_contract.md
- docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md
- docs/prompt_queues/backend_test_coverage.md
- src/MathLearning.Api/Endpoints/CoinEndpoints.cs
- src/MathLearning.Api/Endpoints/HintEndpoints.cs
- src/MathLearning.Api/Endpoints/PowerupEndpoints.cs
- tests/MathLearning.Tests/Endpoints/LegacyEconomyCompatibilityEndpointsTests.cs
- .ai/runs/2026-07-14-BACKEND-API-DB-010-evidence.md

## Commands run

- Get-Content docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md
- Get-Content docs/prompt_queues/backend_test_coverage.md
- Get-Content docs/ai/learning/MISTAKE_LEDGER.md
- git status --short
- Get-Content src/MathLearning.Api/Endpoints/CoinEndpoints.cs
- Get-Content src/MathLearning.Api/Endpoints/HintEndpoints.cs
- Get-Content src/MathLearning.Api/Endpoints/PowerupEndpoints.cs
- Get-Content src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs
- Get-Content docs/BACKEND_ROUTE_COMPATIBILITY_AUDIT.md
- Get-Content tests/MathLearning.Tests/Contracts/MobileMutationContractIntegrationTests.cs
- Get-Content tests/MathLearning.Tests/Idempotency/EconomyOperationIdIdempotencyTests.cs
- dotnet build tests/MathLearning.Tests/MathLearning.Tests.csproj -nologo
- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~LegacyEconomyCompatibilityEndpointsTests|FullyQualifiedName~EconomySettlementEndpointsIntegrationTests|FullyQualifiedName~EconomyOperationIdIdempotencyTests|FullyQualifiedName~MobileEconomyContractIntegrationTests" --no-build -nologo
- rg -n "..." docs/API_ENDPOINT_INVENTORY.md docs/backend_contract_gap_report.md docs/mobile_api_contract.md docs/mobile_economy_api_contract.md docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md docs/prompt_queues/backend_test_coverage.md .ai/runs/2026-07-14-BACKEND-API-DB-010-evidence.md

## What was done

- Claimed BACKEND-API-DB-010 as the next backend C# queue prompt after BACKEND-API-DB-009 without touching Admin project.
- Converted legacy `POST /api/coins/earn`, `POST /api/coins/spend`, `POST /api/powerups/streak-freeze/buy`, and legacy `/api/questions/{id}/hint/*` bypass aliases into explicit `410 Gone` compatibility responses with replacement routes and a `Sunset` header.
- Kept `/api/coins/balance|history|leaderboard` as compatibility read models only; rewired `/api/coins/history` to project completed canonical `EconomyTransaction` rows instead of inferring pseudo-history from hint usage.
- Made canonical `GET /api/hints/questions/{id}/formula|clue|solution` read-only and unlock-aware: locked reads no longer debit coins or create `UserHint` rows, and unlocked reads replay stored content.
- Made canonical `POST /api/hints/questions/{id}/eliminate` read-only unless the hint was already unlocked via `/api/economy/hints/use`.
- Added focused regression coverage in `LegacyEconomyCompatibilityEndpointsTests.cs` for removed legacy mutations, paid-hint bypass removal, read-only canonical hint retrieval, and compatibility history projection.
- Updated endpoint inventory, contract docs, gap report, and queue/test-coverage rows to reflect the runtime-fixed state and required consumer follow-up.

## What was missed

- none yet

## Validation run

- `dotnet build tests/MathLearning.Tests/MathLearning.Tests.csproj -nologo`
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~LegacyEconomyCompatibilityEndpointsTests|FullyQualifiedName~EconomySettlementEndpointsIntegrationTests|FullyQualifiedName~EconomyOperationIdIdempotencyTests|FullyQualifiedName~MobileEconomyContractIntegrationTests" --no-build -nologo`

## Validation not run

- No GitHub Actions evidence found via connector.

## Waste categories

- none

## Mistakes observed

- none

## Queue updated

- `docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md`
- `docs/prompt_queues/backend_test_coverage.md`

## Completion %

- 100

## Residual risk

- Compatibility read surfaces remain in place, so consumers still need to move fully off legacy `/api/coins/*` reads if full route retirement is desired.
- `BuildEliminatePayload` currently uses process-local `GetHashCode()` seeding for deterministic replay within a running app instance; if cross-process stable elimination payloads become contract-critical, replace it with a stable hash.

## Commit SHA

- none

# BACKEND-API-DB-020 Evidence

Prompt ID: BACKEND-API-DB-020
Queue: docs/prompt_queues/backend_cross_repo_current_main_2026_07_17.md
Agent/tool: Codex / functions.exec_command
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix implementation + endpoint/service/test validation
Token budget: medium
Actual context: private bug screenshot route and opaque storage key contract
Started from queue status: Ready
Local collision check: no visible branch or PR collision found for BACKEND-API-DB-020
Relevant prior mistakes read:
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001
- BACKEND-MISTAKE-AUTH-001
How this run avoids prior mistakes:
- keep the bug screenshot contract explicit, prove reporter/admin access with executable tests, and record any storage/provider split instead of treating a public path as the contract.
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Files inspected

- `docs/prompt_queues/README.md`
- `docs/prompt_queues/PROMPT_LIFECYCLE.md`
- `docs/prompt_queues/backend_cross_repo/BACKEND-API-DB-020.md`
- `docs/prompt_queues/backend_cross_repo_current_main_2026_07_17.md`
- `docs/BUGFIX_PATTERN_GUARDRAILS.md`
- `src/MathLearning.Api/Endpoints/BugEndpoints.cs`
- `src/MathLearning.Infrastructure/Services/BugReportService.cs`
- `src/MathLearning.Infrastructure/Services/LocalScreenshotStorageService.cs`
- `src/MathLearning.Application/Services/IBugReportService.cs`
- `src/MathLearning.Application/DTOs/Bugs/BugDtos.cs`
- `src/MathLearning.Domain/Entities/BugReport.cs`
- `tests/MathLearning.Tests/Endpoints/BugEndpointAuthorizationTests.cs`
- `tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs`
- `src/MathLearning.Api/Program.cs`
- `docs/API_ENDPOINT_INVENTORY.md`

## Files changed

- `docs/API_ENDPOINT_INVENTORY.md`
- `src/MathLearning.Api/Endpoints/BugEndpoints.cs`
- `src/MathLearning.Application/DTOs/Bugs/BugDtos.cs`
- `src/MathLearning.Application/Services/IBugReportService.cs`
- `src/MathLearning.Infrastructure/Services/BugReportService.cs`
- `src/MathLearning.Infrastructure/Services/LocalScreenshotStorageService.cs`
- `tests/MathLearning.Tests/Endpoints/BugEndpointAuthorizationTests.cs`
- `tests/MathLearning.Tests/Services/BugReportServicePaginationTests.cs`
- `tests/MathLearning.Tests/Endpoints/BugScreenshotEndpointIntegrationTests.cs`
- `tests/MathLearning.Tests/Services/BugScreenshotStorageTests.cs`
- `docs/prompt_queues/backend_cross_repo_current_main_2026_07_17.md`

## Commands run

- `git status --short`
- `rg -n "ScreenshotUrl|LocalScreenshotStorageService|BugReportService|IScreenshotStorageService|IBugReportService|BugEndpointAuthorizationTests|BugEndpoints" src tests -S`
- `Get-Content docs/BUGFIX_PATTERN_GUARDRAILS.md`
- `Get-Content src/MathLearning.Api/Endpoints/BugEndpoints.cs`
- `Get-Content src/MathLearning.Infrastructure/Services/BugReportService.cs`
- `Get-Content src/MathLearning.Infrastructure/Services/LocalScreenshotStorageService.cs`
- `Get-Content src/MathLearning.Application/Services/IBugReportService.cs`
- `Get-Content src/MathLearning.Application/DTOs/Bugs/BugDtos.cs`
- `Get-Content tests/MathLearning.Tests/Endpoints/BugEndpointAuthorizationTests.cs`
- `Get-Content tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs`
- `Get-Content tests/MathLearning.Tests/Services/BugReportServicePaginationTests.cs`
- `Get-Content docs/API_ENDPOINT_INVENTORY.md`
- `dotnet build MathLearning.slnx -c Release`
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~BugScreenshot|FullyQualifiedName~BugEndpointAuthorization|FullyQualifiedName~BugReportServicePagination"`
- `dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext`
- `git diff --check`

## What was done

- Replaced persisted/public bug screenshot URL semantics with opaque storage keys in the domain/service layer.
- Added an authenticated `/api/bugs/{id:guid}/screenshot` byte-stream route that authorizes the reporter or an admin before reading storage.
- Kept the retained `ScreenshotUrl` field as an authorized API route, not a storage path, so existing DTO consumers stay on the same field shape.
- Added cleanup on bug-save failure so uploaded screenshots do not orphan when the database write fails.
- Added regression tests for reporter/admin/cross-user/anonymous access, DTO route shape, save-failure cleanup, and storage key/path traversal rejection.
- Updated `docs/API_ENDPOINT_INVENTORY.md` to document the private screenshot route and the authorized route semantics.
- No mobile contract update was needed because bug-report screenshots are backend/admin-only and the mobile contract does not surface screenshot URLs.

## What was missed

- The durable provider/deployment migration for screenshot storage topology is still owned by `BACKEND-API-DB-021`.

## Validation run

- `dotnet build MathLearning.slnx -c Release` passed.
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~BugScreenshot|FullyQualifiedName~BugEndpointAuthorization|FullyQualifiedName~BugReportServicePagination"` passed with 10 tests.
- `dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext` returned `No changes have been made to the model since the last migration.`
- `git diff --check` passed.
- `No GitHub Actions evidence found via connector`

## Validation not run

- `dotnet format --verify-no-changes` not run - not needed after focused compile/test validation.

## Waste categories

- Old test stub `NoOpScreenshotStorageService` still implemented the pre-change screenshot contract and failed build until updated.
- One pass of compile verification was needed to catch the interface drift in the test-only storage stub.

## Mistakes observed

- none

## Where time/context was wasted

- Recompiling after the old screenshot-storage test stub surfaced the new interface mismatch.

## Why waste happened

- The bugfix changed the storage contract shape, so one legacy test double still needed to be synchronized with the new interface.

## What the next agent should avoid

- Reintroducing public screenshot URLs or storage paths into bug DTOs.
- Treating anonymous `/uploads/screenshots/*` blocking as sufficient privacy control.
- Skipping the reporter/admin byte-stream route when the API contract exposes screenshot access.

## Docs/rules updated to prevent repeat

- `docs/API_ENDPOINT_INVENTORY.md`

## Queue updated

- `BACKEND-API-DB-020` marked `Done` in `docs/prompt_queues/backend_cross_repo_current_main_2026_07_17.md` with run log `.ai/runs/2026-07-22-BACKEND-API-DB-020-evidence.md`.

## New optimized prompt added

- none

## Follow-up prompt

- `BACKEND-API-DB-021` for durable private screenshot provider/deployment selection.

## Completion %

- 100%

## Residual risk

- storage durability/topology remains local-file based until `BACKEND-API-DB-021` is executed.

## Commit SHA

- f5f6300

# BACKEND-API-DB-016 Evidence

Prompt ID: BACKEND-API-DB-016
Queue: docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md
Agent/tool: Codex / functions.exec_command
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: implementation/test
Token budget: medium
Actual context: bug-report screenshot privacy hardening
Started from queue status: Prompt-ready
Local collision check: no existing 2026-07-16 BACKEND-API-DB-016 run log found
Relevant prior mistakes read:
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes:
- fix the anonymous static screenshot exposure first and prove it with a regression test before claiming broader storage redesign.
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Files inspected

- docs/BUGFIX_PATTERN_GUARDRAILS.md
- src/MathLearning.Api/Program.cs
- src/MathLearning.Api/Endpoints/BugEndpoints.cs
- src/MathLearning.Infrastructure/Services/LocalScreenshotStorageService.cs
- src/MathLearning.Infrastructure/Services/BugReportService.cs
- src/MathLearning.Application/Services/IBugReportService.cs
- src/MathLearning.Application/DTOs/Bugs/BugDtos.cs
- src/MathLearning.Domain/Entities/BugReport.cs
- tests/MathLearning.Tests/Endpoints/BugEndpointAuthorizationTests.cs

## Files changed

- src/MathLearning.Api/Program.cs
- tests/MathLearning.Tests/Endpoints/BugEndpointAuthorizationTests.cs
- .ai/runs/2026-07-16-BACKEND-API-DB-016-evidence.md

## Commands run

- Get-Content docs/BUGFIX_PATTERN_GUARDRAILS.md
- Get-Content src/MathLearning.Api/Program.cs
- Get-Content src/MathLearning.Api/Endpoints/BugEndpoints.cs
- Get-Content src/MathLearning.Infrastructure/Services/LocalScreenshotStorageService.cs
- Get-Content src/MathLearning.Infrastructure/Services/BugReportService.cs
- Get-Content src/MathLearning.Application/Services/IBugReportService.cs
- Get-Content src/MathLearning.Application/DTOs/Bugs/BugDtos.cs
- Get-Content src/MathLearning.Domain/Entities/BugReport.cs
- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "BugEndpointAuthorizationTests"

## What was done

- Added a deny rule for `/uploads/screenshots/*` in the existing static-file middleware so bug screenshot bytes are no longer anonymously fetchable from the public uploads tree.
- Added a regression test that creates a real screenshot file under the test content root and verifies anonymous access returns 404.
- Left the existing bug-report authorization behavior intact.

## What was missed

- The full durable/private attachment redesign from the prompt remains open: screenshots are still stored on local disk and the DTO/service still carry a screenshot URL.

## Validation run

- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "BugEndpointAuthorizationTests" — passed

## Validation not run

- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~BugScreenshot|FullyQualifiedName~BugReport|FullyQualifiedName~BugEndpointAuthorization" - not run
- dotnet build MathLearning.slnx -c Release - not run
- dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext - not run

## Waste categories

- partial-scope fix
- storage redesign deferred

## Mistakes observed

- none

## Where time/context was wasted

- I had to stop at the smallest high-risk privacy fix instead of the full screenshot storage redesign required by the prompt.

## Why waste happened

- The prompt scope is larger than the currently feasible narrow fix without adding a new storage/provider design.

## What the next agent should avoid

- Do not claim the screenshot storage redesign is complete just because anonymous `/uploads/screenshots/*` now returns 404.

## Docs/rules updated to prevent repeat

- none

## Queue updated

- none

## New optimized prompt added

- none

## Follow-up prompt

- BACKEND-API-DB-016 still needs a durable/private attachment storage redesign and authorized read path if the queue is to be fully completed.

## Completion %

- 70%

## Residual risk

- screenshots are still stored on local disk and exposed as persisted URLs in bug report data, so the durable/private attachment redesign is still open.

## Commit SHA

- uncommitted

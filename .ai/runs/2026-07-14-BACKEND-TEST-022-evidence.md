# BACKEND-TEST-022 Evidence

Prompt ID: BACKEND-TEST-022
Queue: docs/prompt_queues/backend_test_coverage.md
Agent/tool: Codex
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: runtime architecture + migration + tests
Token budget: unknown-not-exposed
Actual context: Make quiz/offline attempt ingest durable and idempotent after authoritative settlement using existing backend infrastructure.
Started from queue status: Prompt-ready
Local collision check: Clean worktree at prompt start after prior commit; no local collision detected.
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-QUEUE-001
How this run avoids prior mistakes: Created evidence file before edits, selected the next canonical backend owner from queue order, and will record exact validation commands and queue updates.
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Files inspected

- docs/prompt_queues/backend_test_coverage.md
- docs/prompt_queues/backend_test_followups_2026_07_03.md
- src/MathLearning.Api/Endpoints/QuizEndpoints.cs
- src/MathLearning.Api/Services/QuizAttemptIngestService.cs
- src/MathLearning.Application/Services/IQuizAttemptIngestService.cs
- src/MathLearning.Domain/Entities/WeaknessAnalysisModels.cs
- src/MathLearning.Domain/Entities/UserAnswerAudit.cs
- src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs
- src/MathLearning.Api/Endpoints/ApiDbTransactionHelpers.cs
- src/MathLearning.Api/Services/PracticeAnalyticsUpdater.cs
- tests/MathLearning.Tests/Services/QuizAttemptIngestServiceRelationalTests.cs
- tests/MathLearning.Tests/Services/WeaknessAnalysisServiceIntegrationTests.cs
- tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs
- tests/MathLearning.Tests/Contracts/OperationIdentityContractIntegrationTests.cs

## Files changed

- .ai/runs/2026-07-14-BACKEND-TEST-022-evidence.md
- docs/backend_contract_gap_report.md
- docs/prompt_queues/backend_test_coverage.md
- docs/prompt_queues/backend_test_followups_2026_07_03.md
- src/MathLearning.Api/Endpoints/QuizEndpoints.cs
- src/MathLearning.Api/Services/QuizAttemptIngestOutbox.cs
- src/MathLearning.Api/Services/QuizAttemptIngestService.cs
- src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs
- src/MathLearning.Application/Services/IQuizAttemptIngestService.cs
- src/MathLearning.Domain/Entities/WeaknessAnalysisModels.cs
- src/MathLearning.Domain/Events/QuizAttemptIngestRequested.cs
- src/MathLearning.Infrastructure/Migrations/Api/20260714132651_AddQuizAttemptIngestAttemptKey.cs
- src/MathLearning.Infrastructure/Migrations/Api/20260714132651_AddQuizAttemptIngestAttemptKey.Designer.cs
- src/MathLearning.Infrastructure/Migrations/Api/ApiDbContextModelSnapshot.cs
- src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs
- src/MathLearning.Infrastructure/Services/EventBus/Handlers/QuizAttemptIngestRequestedHandler.cs
- tests/MathLearning.Tests/Endpoints/DurableQuizAttemptIngestEndpointTests.cs
- tests/MathLearning.Tests/Services/QuizAttemptIngestServiceRelationalTests.cs
- tests/MathLearning.Tests/Services/WeaknessAnalysisServiceIntegrationTests.cs

## Commands run

- rg -n "BACKEND-TEST-022|BACKEND-TEST-023|BACKEND-API-DB-004|Re-run BACKEND|Run BACKEND" docs/prompt_queues/backend_test_coverage.md
- rg -n "BACKEND-TEST-022|## BACKEND-TEST-022|BACKEND-TEST-023" docs/prompt_queues/backend_test_followups_2026_07_03.md
- Get-Content docs/prompt_queues/backend_test_followups_2026_07_03.md -TotalCount 220
- Get-Content src/MathLearning.Api/Endpoints/QuizEndpoints.cs
- Get-Content src/MathLearning.Api/Services/QuizAttemptIngestService.cs
- Get-Content src/MathLearning.Application/Services/IQuizAttemptIngestService.cs
- rg -n "IngestAttemptsAsync|QuizAttemptIngestItem|QuizAttempt" src tests
- rg -n "class QuizAttempt|UserTopicStat|UserSubtopicStat|QuizAttempts" src/MathLearning.Domain src/MathLearning.Infrastructure
- Get-Content tests/MathLearning.Tests/Services/QuizAttemptIngestServiceRelationalTests.cs
- Get-Content src/MathLearning.Domain/Entities/WeaknessAnalysisModels.cs
- Get-Content src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs | Select-String -Pattern "builder.Entity<QuizAttempt>|builder.Entity<UserTopicStat>|builder.Entity<UserSubtopicStat>" -Context 0,35
- Get-Content src/MathLearning.Domain/Events/IDomainEvent.cs
- Get-Content src/MathLearning.Domain/Events/DomainEventBase.cs
- Get-Content src/MathLearning.Domain/Events/QuizCompleted.cs
- Get-Content src/MathLearning.Api/Endpoints/ApiDbTransactionHelpers.cs
- dotnet build src/MathLearning.Api/MathLearning.Api.csproj -nologo
- dotnet ef migrations add AddQuizAttemptIngestAttemptKey --context ApiDbContext --project src/MathLearning.Infrastructure --startup-project src/MathLearning.Api --output-dir Migrations/Api
- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~DurableQuizAttemptIngestEndpointTests" --no-restore -nologo
- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "QuizAttemptIngest|OfflineBatch|QuizAnswer|Outbox" --no-restore -nologo
- dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext
- powershell -ExecutionPolicy Bypass -File scripts/db/validate-schema.ps1
- git diff --check -- <BACKEND-TEST-022 touched files>

## What was done

- Selected BACKEND-TEST-022 as the next backend C# non-admin queue prompt after BACKEND-TEST-023 moved to workflow-validation state.
- Replaced inline post-commit analytics ingest calls with transactional outbox enqueue for quiz answer and offline batch settlement.
- Added `QuizAttemptIngestRequested` event/handler and `AttemptKey`-based ingest dedupe on `quiz_attempt`.
- Added endpoint tests proving settlement returns success while ingest remains pending/recoverable, plus relational ingest tests for duplicate-key safety and missing-subtopic handling.
- Generated the `AddQuizAttemptIngestAttemptKey` migration and updated the snapshot.
- Updated queue and contract docs to describe the new durable ingest semantics.

## What was missed

- none

## Validation run

- `dotnet build src/MathLearning.Api/MathLearning.Api.csproj -nologo` (passed; existing OpenTelemetry NU1902 warnings remain)
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~DurableQuizAttemptIngestEndpointTests" --no-restore -nologo` (passed: 2/2)
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "QuizAttemptIngest|OfflineBatch|QuizAnswer|Outbox" --no-restore -nologo` (passed: 42/42)
- `dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext` (passed: no pending model changes)
- `git diff --check -- <BACKEND-TEST-022 touched files>` (passed)

## Validation not run

- `powershell -ExecutionPolicy Bypass -File scripts/db/validate-schema.ps1` - timed out because no reachable local PostgreSQL instance was available
- CI: No GitHub Actions evidence found via connector

## Waste categories

- none

## Mistakes observed

- none

## Where time/context was wasted

- none

## Why waste happened

- none

## What the next agent should avoid

- Do not split durable ingest work into a second owner if the same outbox-based pattern can satisfy it here.

## Docs/rules updated to prevent repeat

- none

## Queue updated

- `docs/prompt_queues/backend_test_coverage.md`
- `docs/prompt_queues/backend_test_followups_2026_07_03.md`

## New optimized prompt added

- none

## Follow-up prompt

- none

## Completion %

- 90

## Residual risk

- Full schema validation script still needs a reachable local or CI PostgreSQL instance.
- Delivery is now durable and idempotent, but analytics lag can still persist until the outbox worker drains pending ingest events.

## Commit SHA

- none

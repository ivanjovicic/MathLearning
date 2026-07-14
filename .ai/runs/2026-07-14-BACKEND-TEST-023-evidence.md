# BACKEND-TEST-023 Evidence

Prompt ID: BACKEND-TEST-023
Queue: docs/prompt_queues/backend_test_coverage.md
Agent/tool: Codex
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: refactor + relational concurrency tests
Token budget: unknown-not-exposed
Actual context: Implement canonical outbox claim/lease, duplicate-publish protection, bounded retries, dead-letter handling, and regression coverage for the backend runtime owner prompt.
Started from queue status: Prompt-ready
Local collision check: Existing dirty worktree detected, including prior in-progress BACKEND-TEST-032 files; avoided reverting unrelated changes.
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-QUEUE-001
How this run avoids prior mistakes: Created run log before edits, selected the canonical queue owner before coding, and will record exact validation commands plus queue updates.
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Files inspected

- AGENTS.md
- docs/BUGFIX_PATTERN_GUARDRAILS.md
- docs/ai/learning/MISTAKE_LEDGER.md
- docs/prompt_queues/backend_test_coverage.md
- docs/prompt_queues/backend_test_followups_2026_07_03.md
- docs/prompt_queues/backend_performance_followups_2026_07_03.md
- docs/AGENT_RUN_LOG_ENFORCEMENT.md
- .ai/RUN_LOG_TEMPLATE.md
- src/MathLearning.Infrastructure/Services/EventBus/OutboxProcessor.cs
- src/MathLearning.Infrastructure/Persistance/Models/OutboxMessage.cs
- src/MathLearning.Infrastructure/Persistance/AppDbContext.cs

## Files changed

- .ai/runs/2026-07-14-BACKEND-TEST-023-evidence.md
- docs/backend_contract_gap_report.md
- docs/prompt_queues/backend_performance_followups_2026_07_03.md
- docs/prompt_queues/backend_test_coverage.md
- docs/prompt_queues/backend_test_followups_2026_07_03.md
- src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs
- src/MathLearning.Infrastructure/Migrations/Api/20260714124712_AddOutboxRetryAndDeadLetter.cs
- src/MathLearning.Infrastructure/Migrations/Api/20260714124712_AddOutboxRetryAndDeadLetter.Designer.cs
- src/MathLearning.Infrastructure/Migrations/Api/ApiDbContextModelSnapshot.cs
- src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs
- src/MathLearning.Infrastructure/Persistance/AppDbContext.cs
- src/MathLearning.Infrastructure/Persistance/Models/OutboxMessage.cs
- src/MathLearning.Infrastructure/Properties/AssemblyInfo.cs
- src/MathLearning.Infrastructure/Services/EventBus/OutboxBatchProcessor.cs
- src/MathLearning.Infrastructure/Services/EventBus/OutboxProcessingOptions.cs
- src/MathLearning.Infrastructure/Services/EventBus/OutboxProcessor.cs
- tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs
- tests/MathLearning.Tests/Helpers/PostgresTestDatabase.cs
- tests/MathLearning.Tests/Helpers/PostgresWebApplicationFactory.cs
- tests/MathLearning.Tests/Infrastructure/OutboxBatchProcessorTests.cs

## Commands run

- Get-Content .ai/RUN_LOG_TEMPLATE.md
- Get-Content src/MathLearning.Infrastructure/Services/EventBus/OutboxProcessor.cs
- rg -n "class OutboxMessage|OutboxMessage|Outbox" src tests/MathLearning.Tests
- Get-Content src/MathLearning.Infrastructure/Persistance/Models/OutboxMessage.cs
- Get-Content src/MathLearning.Infrastructure/Persistance/AppDbContext.cs | Select-String -Pattern "Outbox" -Context 4,20
- Get-Content docs/prompt_queues/backend_performance_followups_2026_07_03.md | Select-String -Pattern "BE-PERF-016" -Context 0,40
- Get-Content docs/AGENT_RUN_LOG_ENFORCEMENT.md | Select-String -Pattern "non-trivial|run log|Done" -Context 1,3
- Get-Content src/MathLearning.Infrastructure/Migrations/Api/20260213083507_AddOutboxTable.cs
- Get-Content tests/MathLearning.Tests/Helpers/PostgresTestDatabase.cs
- Get-Content tests/MathLearning.Tests/Helpers/PostgresWebApplicationFactory.cs
- Get-Content src/MathLearning.Infrastructure/Persistance/AppDbContext.cs
- Get-Content src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs | Select-String -Pattern "builder.Entity<OutboxMessage>" -Context 0,20
- Get-Content tests/MathLearning.Tests/Infrastructure/PostgresProviderValidationTests.cs
- Get-Content tests/MathLearning.Tests/Endpoints/AuthRefreshPostgresConcurrencyTests.cs
- Get-Content tests/MathLearning.Tests/Endpoints/ApiDbTransactionHelpersRelationalTests.cs
- Get-Content src/MathLearning.Infrastructure/Services/EventBus/InProcEventBus.cs
- Get-Content src/MathLearning.Infrastructure/Services/EventBus/IEventBus.cs
- rg -n "AddHostedService<OutboxProcessor>|AddSingleton<IEventBus>|AddScoped<IEventBus>|InProcEventBus|OutboxProcessor" src
- Get-Content src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs | Select-String -Pattern "IEventBus|HostedService|Outbox" -Context 3,3
- rg -n "AddDbContext<AppDbContext>|AppDbContext\(" src/MathLearning.Api src/MathLearning.Infrastructure
- rg -n "DomainEvents|AddDomainEvent|QuizCompleted|CoinsGranted|StreakProtectedByFreeze" src
- rg -n "Outbox|outbox" docs/backend_contract_gap_report.md docs/ARCHITECTURE_OVERVIEW.md docs/API_ENDPOINT_INVENTORY.md
- dotnet --version
- Get-Content tests/MathLearning.Tests/MathLearning.Tests.csproj
- dotnet build src/MathLearning.Api/MathLearning.Api.csproj -nologo
- dotnet ef migrations add AddOutboxRetryAndDeadLetter --context ApiDbContext --project src/MathLearning.Infrastructure --startup-project src/MathLearning.Api --output-dir Migrations/Api
- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Outbox" --no-restore -nologo
- $env:POSTGRES_PROVIDER_TESTS_REQUIRED='1'; dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~OutboxBatchProcessorTests" --no-restore -nologo
- git diff --check
- git rev-parse HEAD

## What was done

- Created the mandatory evidence log before implementation.
- Confirmed BACKEND-TEST-023 is the canonical runtime owner for the outbox concurrency lane.
- Inspected the current processor and schema to identify missing claim/lease, retry, and dead-letter support.
- Replaced the inline polling logic with a scoped `OutboxBatchProcessor` that claims rows inside a PostgreSQL transaction using `FOR UPDATE SKIP LOCKED`.
- Added `NextAttemptUtc` and `DeadLetteredUtc` outbox state, bounded retry/backoff rules, and redacted/truncated persisted error messages.
- Registered the outbox hosted service in startup and excluded it from in-memory test hosts where relational locking is unavailable.
- Generated migration `20260714124712_AddOutboxRetryAndDeadLetter` plus snapshot updates for the shared API schema.
- Added provider-gated outbox regression tests for concurrent claiming, retry/dead-letter progression, cancellation rollback/recovery, missing-table behavior, and error redaction.
- Updated queue/docs state to reflect implementation completion with workflow validation still pending.

## What was missed

- none

## Validation run

- `dotnet build src/MathLearning.Api/MathLearning.Api.csproj -nologo` (passed; existing OpenTelemetry NU1902 warnings remain)
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Outbox" --no-restore -nologo` (passed: 5/5)
- `$env:POSTGRES_PROVIDER_TESTS_REQUIRED='1'; dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~OutboxBatchProcessorTests" --no-restore -nologo` (failed: local PostgreSQL auth `28P01 password authentication failed for user "postgres"`)
- `git diff --check -- <BACKEND-TEST-023 touched files>` (passed)

## Validation not run

- GitHub Actions provider proof - not run locally; No GitHub Actions evidence found via connector
- repo-wide `git diff --check` - not a useful signal for this prompt because unrelated pre-existing whitespace issues exist in other dirty files

## Waste categories

- none

## Mistakes observed

- none

## Where time/context was wasted

- none

## Why waste happened

- none

## What the next agent should avoid

- Do not implement outbox concurrency in a secondary prompt; BACKEND-TEST-023 is the canonical owner.

## Docs/rules updated to prevent repeat

- none

## Queue updated

- `docs/prompt_queues/backend_test_coverage.md`
- `docs/prompt_queues/backend_test_followups_2026_07_03.md`
- `docs/prompt_queues/backend_performance_followups_2026_07_03.md`

## New optimized prompt added

- none

## Follow-up prompt

- none

## Completion %

- 90

## Residual risk

- Hosted outbox processing is now active in runtime, but the real PostgreSQL lock/claim proof still needs CI evidence or valid local maintenance credentials.
- Delivery semantics remain at-least-once; idempotent consumers are still required for publish-after-side-effect failure windows.

## Commit SHA

- none

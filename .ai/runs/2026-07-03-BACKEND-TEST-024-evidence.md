# BACKEND-TEST-024 Evidence

Prompt ID: BACKEND-TEST-024
Queue: `docs/prompt_queues/backend_test_followups_2026_07_03.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: maintenance refactor + endpoint contract tests
Started from queue status: Prompt-ready

## Goal

Make maintenance operations injectable and testable, ensure HTTP and CLI statistics paths are side-effect free, add cancellation and non-overlap semantics, and cover positive admin behavior without touching a real PostgreSQL database.

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-AUTH-002
- BACKEND-MISTAKE-PERF-001

## Confirmed problem

- Endpoints instantiated `IndexMaintenanceService` directly.
- `GET /api/maintenance/index-stats` called `RebuildCorruptedIndexesAsync`, which could execute `REINDEX` and `ANALYZE` from a GET request.
- CLI `stats` also called the mutating rebuild method.
- Service methods did not accept cancellation tokens.
- Scheduled and manual paths created separate service instances and had no shared in-process overlap guard.

## Files changed

- `src/MathLearning.Infrastructure/Maintenance/IndexMaintenanceService.cs`
- `src/MathLearning.Infrastructure/DependencyInjection.cs`
- `src/MathLearning.Api/Services/IndexMaintenanceBackgroundService.cs`
- `src/MathLearning.Api/Endpoints/MaintenanceEndpoints.cs`
- `IndexMaintenanceTool/Program.cs`
- `tests/MathLearning.Tests/Endpoints/MaintenanceEndpointContractTests.cs`
- related inventory/audit/queue/learning docs.

## Runtime changes

- Introduced `IIndexMaintenanceService`.
- Registered one singleton implementation shared by HTTP and hosted worker paths.
- Split read-only `GetIndexStatisticsAsync` from mutating `RebuildCorruptedIndexesAsync`.
- HTTP GET and CLI `stats` no longer invoke rebuild or `ANALYZE`.
- Added cancellation tokens to connection open, reads, commands and endpoints.
- Added shared in-process `SemaphoreSlim` rebuild guard.
- Quoted index/table identifiers before maintenance commands.
- Replaced direct service construction in hosted worker/endpoints with DI.

## New tests

Four endpoint contract tests:

1. Admin index statistics calls only read-only statistics and never rebuild.
2. Admin index health returns stable counts and calls only the health read.
3. Admin rebuild invokes mutation exactly once and returns the report.
4. A rebuild report with item errors returns `success=false` while preserving safe report data.

Existing anonymous/non-admin/policy-metadata tests remain applicable.

## Static validation

- Test factory replaces `IIndexMaintenanceService` with a recording singleton.
- Positive admin tests do not connect to PostgreSQL.
- GET statistics asserts zero rebuild calls.
- The custom factory removes the maintenance hosted worker.
- CLI `stats` now directly calls `GetIndexStatisticsAsync`.

## Validation not run

No executable repository/.NET SDK is available. No build or passing-test claim is made.

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "MaintenanceEndpoint|IndexMaintenance"
dotnet build MathLearning.slnx -c Release
```

## Residual risk

- Local semaphore is not a distributed lock.
- Real PostgreSQL rebuild/cancellation behavior is untested.
- Actor/correlation audit is missing.
- Detailed item errors need safer projection.
- The current bloat metric may always produce zero; BACKEND-TEST-048 requires PostgreSQL proof or replacement.

Follow-ups: BACKEND-TEST-042 and BACKEND-TEST-048.

## Completion

75%

Commit SHA: 930e45b18b4637237ec86efc911298c1bf80c935

## Key commits

- `0f702e9c5a9c680a0efa84987b4f8bcc350c5390` — start evidence
- `825ee400702df8dad6ac46d3e8d22a74d532c6e5` — maintenance interface/read-only split/cancellation
- `b63867216bf036e1b88694fe51b85c8fb023f4b0` — DI registration
- `65642fb72f3e97537397e37a01be46125588477a` — shared hosted worker service
- `519596113edbbe04fb029756ff04c0e01fb113c5` — injectable endpoint paths
- `c068a0fb70d81c928d99bf3ddcaffc2f28868756` — positive admin tests
- `c0e670d02ce2d20e6a09554bb15fdb509c17fa57` — read-only CLI statistics
- `5cd15fed0aaba2fc39e45a117fd80200daafc9cd` — PostgreSQL bloat-metric prompt

## Cross-repo sync

Not applicable. Maintenance is backend/admin operational behavior.

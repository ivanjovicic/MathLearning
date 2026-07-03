# BACKEND-TEST-024 Evidence

Prompt ID: BACKEND-TEST-024
Queue: `docs/prompt_queues/backend_test_followups_2026_07_03.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: maintenance refactor + endpoint contract tests
Started from queue status: Prompt-ready

## Goal

Make maintenance operations injectable and testable, ensure GET routes are side-effect free, add cancellation and non-overlap semantics, and cover positive admin behavior without touching a real PostgreSQL database.

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-AUTH-002

## Confirmed problem

- Endpoints instantiated `IndexMaintenanceService` directly.
- `GET /api/maintenance/index-stats` called `RebuildCorruptedIndexesAsync`, which could execute `REINDEX` and `ANALYZE` from a GET request.
- Service methods did not accept cancellation tokens.
- Scheduled and manual paths created separate service instances and had no shared in-process overlap guard.

## Files changed

- `src/MathLearning.Infrastructure/Maintenance/IndexMaintenanceService.cs`
- `src/MathLearning.Infrastructure/DependencyInjection.cs`
- `src/MathLearning.Api/Services/IndexMaintenanceBackgroundService.cs`
- `src/MathLearning.Api/Endpoints/MaintenanceEndpoints.cs`
- `tests/MathLearning.Tests/Endpoints/MaintenanceEndpointContractTests.cs`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/prompt_queues/backend_test_followups_pass2_2026_07_03.md`

## Runtime changes

- Introduced `IIndexMaintenanceService`.
- Registered one singleton implementation shared by HTTP and hosted service paths.
- Split read-only `GetIndexStatisticsAsync` from mutating `RebuildCorruptedIndexesAsync`.
- `GET /api/maintenance/index-stats` no longer invokes rebuild or `ANALYZE`.
- Added cancellation tokens to connection open, reads, commands and endpoint calls.
- Added a shared in-process `SemaphoreSlim` rebuild guard.
- Quoted index/table identifiers before `REINDEX`/`ANALYZE` commands.
- Replaced direct service construction in the hosted worker and endpoints with DI.

## New tests

Four endpoint contract tests:

1. Admin index statistics calls only read-only statistics and never rebuild.
2. Admin index health returns stable counts and calls only the health read.
3. Admin rebuild invokes mutation exactly once and returns the report.
4. A completed rebuild report with item errors returns `success=false` while preserving safe report data.

Existing maintenance anonymous/non-admin/policy-metadata tests remain applicable.

## Static validation

- Test factory replaces `IIndexMaintenanceService` with a recording singleton.
- Positive admin tests do not connect to PostgreSQL.
- Cancellation token forwarding is recorded by the fake.
- GET statistics assertions explicitly require zero rebuild calls.
- Custom test factory removes the maintenance hosted service, preventing background interference.

## Validation not run

No executable repository/.NET SDK is available in this connector session. No build or passing-test claim is made.

Required validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "MaintenanceEndpoint|IndexMaintenance"
dotnet build MathLearning.slnx -c Release
```

## Residual risk

- `SemaphoreSlim` protects only one process; multiple replicas still need a PostgreSQL advisory lock or durable lease.
- Real `REINDEX CONCURRENTLY` and cancellation behavior require PostgreSQL integration tests.
- Manual actor/correlation audit is not persisted.
- Per-index detailed errors should be replaced with bounded safe admin error codes.

Follow-up: BACKEND-TEST-042.

## Completion

88%

## Key commits

- `0f702e9c5a9c680a0efa84987b4f8bcc350c5390` — start evidence
- `825ee400702df8dad6ac46d3e8d22a74d532c6e5` — maintenance interface/read-only split/cancellation
- `b63867216bf036e1b88694fe51b85c8fb023f4b0` — DI registration
- `65642fb72f3e97537397e37a01be46125588477a` — hosted worker uses shared service
- `519596113edbbe04fb029756ff04c0e01fb113c5` — injectable endpoint paths
- `c068a0fb70d81c928d99bf3ddcaffc2f28868756` — positive admin contract tests

## Cross-repo sync

Not applicable. Maintenance is a backend/admin operational surface and no mobile payload changed.

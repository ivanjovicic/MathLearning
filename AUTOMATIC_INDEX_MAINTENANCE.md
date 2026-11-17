# Index maintenance: current backend behavior

> Updated 2026-11-17 after PR #50. This replaces the earlier design draft, which described a physical-bloat estimator and automatic index rebuild that the backend did not implement.

## What the backend measures

- The service reads PostgreSQL index size and scan counters. Those values describe size and observed usage; they do not measure physical bloat.
- Size-filtered rows returned by index statistics have `BloatPercentage = null` and `Status = UNKNOWN`. The health check also reports physical status as `UNKNOWN` while exposing `UsageStatus` separately (`UNUSED`, `LOW_USAGE`, or `USED`).
- `UNKNOWN` is not zero and is not a healthy/rebuild decision. The backend has no bloat threshold or physical-bloat estimator.

## Maintenance behavior

- `GET /api/maintenance/index-health` and `GET /api/maintenance/index-stats` are read-only. The statistics route keeps its legacy `bloatedIndexes` field name for compatibility; the rows are size-filtered observations, not measured bloat.
- `POST /api/maintenance/rebuild-indexes` is an admin-only legacy route. It runs `ANALYZE` for the service's fixed table list and completes after the operation. It does not rebuild indexes; the response states that bloat is `UNKNOWN`, reports `automaticReindexEnabled: false`, and returns an empty rebuilt-index list.
- `IndexMaintenanceBackgroundService`, when enabled by the background-work profile, waits for the next 03:00 UTC schedule and invokes the same `ANALYZE`-only service method. A class or schedule in source does not prove that a particular deployment has this worker enabled.
- The `IndexMaintenanceTool` `check` and `stats` commands are observational. Its legacy `rebuild` command runs `ANALYZE` only and reports that no `REINDEX` was performed.

The application maintenance service does not automatically run `REINDEX` and does not install a bloat-measurement extension. This document contains no production command or deployment instruction.

## Current owners

- Route and authorization inventory: [API endpoint inventory](docs/API_ENDPOINT_INVENTORY.md)
- Static API operation summary: [OpenAPI document](openapi.yaml)
- Implementation: [maintenance service](src/MathLearning.Infrastructure/Maintenance/IndexMaintenanceService.cs), [maintenance endpoints](src/MathLearning.Api/Endpoints/MaintenanceEndpoints.cs), [background worker](src/MathLearning.Api/Services/IndexMaintenanceBackgroundService.cs), and [CLI](IndexMaintenanceTool/Program.cs)
- Regression coverage: [PostgreSQL provider tests](tests/MathLearning.Tests/Infrastructure/IndexBloatPostgresTests.cs), [endpoint contract tests](tests/MathLearning.Tests/Endpoints/MaintenanceEndpointContractTests.cs), and [CLI output tests](tests/MathLearning.Tests/Tools/IndexMaintenanceToolOutputTests.cs)
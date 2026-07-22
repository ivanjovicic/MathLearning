# BACKEND-API-DB-021 Decision

Date: 2026-07-22
Repository: `ivanjovicic/MathLearning`
Prompt ID: `BACKEND-API-DB-021`

## Evidence read

- `src/MathLearning.Infrastructure/Services/LocalScreenshotStorageService.cs`
- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- `src/MathLearning.Api/Program.cs`
- `docker-compose.yml`
- `render.yaml`
- `docs/prompt_queues/backend_cross_repo/BACKEND-API-DB-021.md`

## Decision

No durable provider/topology can be selected from the repository facts alone.

The current API wiring still uses process-local screenshot files through `LocalScreenshotStorageService`, `docker-compose.yml` only defines a persistent volume for Postgres, and `render.yaml` does not define an API disk or any object-storage integration. There is no in-repo S3/blob/object-storage abstraction or deployment contract to reuse.

Because `BACKEND-API-DB-021` requires an evidence-backed durable storage migration decision and the repo does not name an operator-approved provider, the prompt is blocked on missing deployment/provider authority.

## Named handoff

`Platform / infrastructure owner` must select and provision the durable private screenshot backend:

- object storage provider or equivalent durable shared topology;
- bucket/container and lifecycle policy;
- credentials/secrets ownership;
- multi-replica read/write and rollback expectations;
- local migration checkpoint and verification rules.

## Bounded follow-up implementation prompt

After the provider is selected, implement a provider-backed `IScreenshotStorageService` behind the existing authorized `/api/bugs/{id:guid}/screenshot` route, keep `ScreenshotUrl` as an authorized API route only, and add restartable migration plus emulator/provider-backed tests.

## Residual risk

Until the operator selects the durable provider, bug screenshots remain local-file backed and are not proven shared across deploys or replicas.

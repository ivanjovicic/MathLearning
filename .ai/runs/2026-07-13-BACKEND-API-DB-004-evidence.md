# BACKEND-API-DB-004 evidence

- Date: 2026-07-13
- Repo: `C:\Users\Alex\source\repos\MathLearning`
- Prompt: `BACKEND-API-DB-004`
- Commit SHA: pending

## What changed

- Scoped sync event, dead-letter and answer identity by user/device.
- Added canonical payload hashing for sync replay/conflict decisions.
- Moved sync transaction start ahead of mutable cursor/idempotency reads and locked the device sync state row on PostgreSQL.
- Switched dead-letter redrive to the dead-letter ID route and surfaced the ID in the admin DTO.
- Added schema migration and snapshot updates for the scoped indexes and payload-hash columns.

## Validation

- `dotnet build tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release -v minimal`
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~SyncServiceTests|FullyQualifiedName~DatabaseSchemaValidationTests" -v minimal`
- `dotnet build MathLearning.slnx -c Release -v minimal`
- `dotnet ef migrations add AddScopedSyncIdentityAndPayloadHash --project src/MathLearning.Infrastructure --startup-project src/MathLearning.Api --context ApiDbContext --output-dir Migrations/Api`

## Notes

- The test run passed for the targeted sync and schema coverage.
- The solution build passed with existing package/security warnings only.

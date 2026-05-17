# Database Migration Pipeline

## Rules by environment

- Development: `Database:StartupMode=AutoMigrate`. The API may apply pending migrations on startup, but it no longer runs silent repair SQL or ignores migration failures.
- Test and CI: create a fresh database, apply migrations from zero, run schema validation, then run tests.
- Staging and Production: `Database:StartupMode=ValidateExact`. The application does not call `MigrateAsync()` on startup. If the database schema is behind, ahead, or otherwise mismatched, startup fails immediately.

## Official scripts

- `./scripts/db/update-dev.ps1`: apply API migrations to the configured development database and print the exact host, port, database, and user.
- `./scripts/db/drop-dev-db.ps1`: drop and recreate the local development API database. The script refuses to run against non-local hosts.
- `./scripts/db/show-migration-drift.ps1`: print migrations in code and show the applied migrations history (when direct DB query tooling is available) to diagnose local drift.
- `./scripts/db/generate-prod-script.ps1 -FromMigration <current-prod> -ToMigration <target>`: generate the reviewed SQL script to apply in higher environments.
- `./scripts/db/validate-schema.ps1`: create a fresh validation database, apply migrations from zero, run the schema drift tests, and return non-zero on failure.

## Recommended workflow

1. Add or update EF migrations in `src/MathLearning.Infrastructure/Migrations/Api`.
2. Run `./scripts/db/update-dev.ps1` against your local database.
3. Run `./scripts/db/validate-schema.ps1` to prove the schema can be built from zero.
4. Commit the migration and any schema-dependent code together.
5. Before staging or production deployment, generate a reviewed script with `./scripts/db/generate-prod-script.ps1`.
6. Apply that script manually or in a controlled release job.
7. Deploy the application binary only after the database reaches the target migration. The Fly deploy workflow is manual and requires an explicit confirmation that this step is already done.

## Health and startup behavior

- `/health` remains the basic liveness endpoint.
- `/api/health/db` reports connectivity plus summarized schema status.
- `/api/health/ready` returns `503` when the database is unreachable or the schema guard says the runtime schema is not ready.
- If startup fails with `Pending=0` and `UnknownApplied>0`, that is local migration-history drift. For disposable local data, run `./scripts/db/drop-dev-db.ps1`.

## What changed

- Global runtime replacement of the EF migrations SQL generator was removed from the normal API path.
- Startup repair SQL is no longer executed implicitly by the API.
- Migration failures are fail-fast instead of warn-and-continue.
- Translation and auxiliary processes must run against an already migrated database.

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

### Shared history and the known Admin legacy record

When the API and Admin contexts point to the same database, both use the default `__EFMigrationsHistory` table unless a separate history table is configured. The API guard therefore allows exactly one verified legacy Admin record: `20260118125140_InitIdentityAndDomain`. This does not allow arbitrary unknown records; any other applied migration that is absent from the API migration assembly still blocks startup.

Do not delete the legacy history row as a repair. Before a production rollout, verify the record and the Admin-owned tables on a backup/snapshot, then apply the reviewed API migration script. If the record is not present or the database is not shared with Admin, this allow-list must be revisited rather than copied to another environment.

## What changed

- Global runtime replacement of the EF migrations SQL generator was removed from the normal API path.
- Startup repair SQL is no longer executed implicitly by the API.
- Migration failures are fail-fast instead of warn-and-continue.
- Translation and auxiliary processes must run against an already migrated database.

## Production reconciliation runbook: September 2026 API migrations

This runbook covers the API migrations after the historical production baseline `20260908130000_EnsureSyncDeadLetterRedriveSchema`. Production migrations remain a separate, explicitly approved operation. Do not run the production commands below as part of an audit or rehearsal.

### Verified state on 2026-10-22

- The latest repository `main` observed was `26949a9ba284d24c152e9ad04f4c6b80b4e8295c`; the API migration assembly ended at `20260923145307_AddDesignTokenDraftUniqueness`.
- The only migrations after the reported September 8 baseline are `20260919104120_AddIdempotencyLeases` and `20260923145307_AddDesignTokenDraftUniqueness`, in that order.
- Fly release v136 completed on October 19. Its machine started on October 20, then Fly stopped it with `requested_stop=true` and `exit_code=0`. The machine is currently stopped by the configured scale-to-zero policy; the Fly health check says that the machine has not started. The retained application log buffer was empty.
- Production config still selects `ValidateExact`; automatic startup migrations are disabled. The Fly deploy workflow requires an explicit `confirmDatabaseMigrated` input and does not run a release migration command.
- Historical root cause: when the reported logs were emitted, production history ended at the September 8 migration while the deployed API migration set expected the September 19 and September 23 migrations. `DatabaseSchemaVersionGuard` correctly rejected that pending schema in `ValidateExact` mode. The failure was database/code migration drift; startup did not attempt to apply migrations.
- The production migration history rows and Draft row count were not directly queried. The clean machine stop is evidence that the old startup incident may have been cleared, but it does not prove the current database history. Check it with the read-only preflight below before deciding whether any migration is needed.
- The API uses PostgreSQL through Npgsql. The repository identifies Neon as the managed production PostgreSQL service, and Fly has no attached Postgres application. The current connection secret was not read, so its live host, server version, backup retention and Neon project permissions remain unverified. The Fly volume at `/data` stores Data Protection keys, not the database.

### Migration review

| Migration | Operations | Data and availability impact |
|---|---|---|
| `20260919104120_AddIdempotencyLeases` | Adds `AttemptCount integer NOT NULL DEFAULT 0`, nullable `LeaseExpiresAtUtc timestamptz`, and nullable `OwnerToken varchar(64)` to `economy_transactions` and `cosmetics_idempotency_ledger`; creates five non-unique indexes on sync and idempotency tables. | No existing columns, tables or rows are dropped or updated. Existing rows read `AttemptCount=0`; lease and owner fields are null. Each `ALTER TABLE ADD COLUMN` needs a strong table lock. PostgreSQL 11 and later can add a constant default without rewriting the heap; older versions can rewrite the affected tables. Each standard `CREATE INDEX` scans its table and blocks writes to that table while it builds, while reads remain available. Table sizes and PostgreSQL version must be checked before choosing a window. |
| `20260923145307_AddDesignTokenDraftUniqueness` | Creates unique partial index `UX_DesignTokenVersion_Draft` on `DesignTokenVersion("Status") WHERE "Status" = 'Draft'`. | Preserves rows, but fails if two or more rows currently have `Status='Draft'`. It scans the table and blocks writes while building the index. Do not delete or merge rows to make the migration pass. If duplicates exist, stop and obtain a data-owner decision for a data-preserving status resolution. |

The generated PostgreSQL script is [api-20260908130000-to-20260923145307-idempotent.sql](../scripts/db/generated/api-20260908130000-to-20260923145307-idempotent.sql). It was generated with `scripts/db/generate-prod-script.ps1` from the September 8 baseline through the September 23 target. It runs each migration in a transaction and uses `__EFMigrationsHistory` to skip migrations already recorded. Repeatability depends on history matching the actual schema; it is not a repair for partially applied DDL or manually changed history. Never use an EF `Down` script in production: the first migration's `Down` drops the newly added lease columns, which could discard live lease state.

The lock and table-rewrite notes follow the [PostgreSQL 16 `ALTER TABLE` documentation](https://www.postgresql.org/docs/16/sql-altertable.html) and [PostgreSQL 16 `CREATE INDEX` documentation](https://www.postgresql.org/docs/16/sql-createindex.html). Confirm the production server version before applying them to the target environment.

### Read-only production preflight

Run the commands from the repository root. Use a protected libpq service file and password file, or an equivalent approved secret manager. The service file contains the host, database, user and TLS mode; the password file contains the credential and must be access-restricted. Do not put a connection string or password in command history, process arguments, logs or this repository. `fly secrets list` reports secret names only; it does not retrieve a safe migration credential.

```powershell
$env:PGSERVICEFILE = 'C:\secure\mathlearning\pg_service.conf'
$env:PGPASSFILE = 'C:\secure\mathlearning\pgpass.conf'
$db = 'service=mathlearning-prod'

psql --dbname=$db --no-psqlrc --set=ON_ERROR_STOP=1 --command="SELECT current_database(), current_setting('server_version'), current_setting('server_version_num');"
psql --dbname=$db --no-psqlrc --set=ON_ERROR_STOP=1 --command='SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
psql --dbname=$db --no-psqlrc --set=ON_ERROR_STOP=1 --command='SELECT count(*) AS draft_count FROM "DesignTokenVersion" WHERE "Status" = ''Draft'';'
psql --dbname=$db --no-psqlrc --set=ON_ERROR_STOP=1 --command="SELECT schemaname, relname, n_live_tup, pg_size_pretty(pg_total_relation_size(relid)) AS total_size FROM pg_stat_user_tables WHERE relname IN ('economy_transactions','cosmetics_idempotency_ledger','SyncEventLog','SyncDeadLetter','ServerSyncEvent','DesignTokenVersion') ORDER BY relname;"
```

Proceed only when the history is known and the schema agrees with it. The expected API migration sequence from the reported baseline is the baseline, then the two migrations in the table above. The guard may also allow the single documented shared Admin history row `20260118125140_InitIdentityAndDomain`. Stop if the baseline is absent, any other migration is applied or pending, a target object exists without its history row, a required table is missing, or the provider/version differs from the rehearsed target. If both migrations are already applied and the exact-schema readiness check passes, make no database change.

If `draft_count` is greater than one, stop before generating or applying any SQL. Review the affected versions with the product/data owner and agree which existing version should remain Draft and which should receive a valid non-Draft lifecycle status. Preserve both records and their audit history. Then re-run the count and rehearse the exact status change on a disposable copy before production authorization.

### Backup and restore rehearsal

The Neon project, restore window, role permissions and a recent successful restore were not accessible during this audit. Before scheduling production work, the database owner must verify the Neon project/branch, point-in-time restore window, backup retention and access to a restore branch. Neon supports [restoring historical branch state to a new branch](https://api-docs.neon.tech/reference/restoreprojectbranch); use a new isolated branch for rehearsal and recovery, then validate it before any cutover. Do not restore over the live branch as the first recovery step.

Take a logical backup using `pg_dump` as a second recovery artifact. Configure `PGSERVICEFILE` and `PGPASSFILE` as above; keep the files access-restricted. Store the dump in encrypted, access-controlled storage outside the repository and verify both its archive listing and checksum.

```powershell
$backupFile = 'D:\protected-backups\mathlearning-before-202609-migrations.dump'
pg_dump --dbname=$db --format=custom --no-owner --file=$backupFile
if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed; stop before migration.' }
pg_restore --list $backupFile | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Backup archive verification failed.' }
Get-FileHash -Algorithm SHA256 $backupFile
```

Restore the dump into an empty database on an isolated Neon rehearsal branch or disposable PostgreSQL instance, using a separate libpq service name for that target. Do not use `--clean` against production. Require `pg_restore --exit-on-error --single-transaction --no-owner` to complete, compare the migration history and row counts, then run the migration script and application checks on that copy. A restore to a point before the migration can lose writes made after that restore point; reconcile those writes before any production cutover.

### Rehearsal and production execution order

1. Create a disposable PostgreSQL database or isolated Neon branch from a protected pre-migration backup/snapshot when available. Confirm its PostgreSQL major version and migration history. If no pre-migration snapshot exists, create a blank disposable database, use `dotnet ef database update 20260908130000_EnsureSyncDeadLetterRedriveSchema --project src/MathLearning.Infrastructure --startup-project src/MathLearning.Api --context ApiDbContext` to reach the reported baseline, then seed representative rows in the affected tables. Mark that rehearsal as synthetic; it does not verify the production data. Never use the production database name for this rehearsal.
2. Capture row counts for all six tables listed in the preflight query, plus the `DesignTokenVersion` Draft count. Save the results with the change record.
3. Confirm the Draft count is zero or one. Apply the generated script to the copy with `psql --set=ON_ERROR_STOP=1 --file <script>`. Run it a second time to prove history-based repeatability. Compare row counts and existing row identifiers before and after; check all new columns and indexes; start the API against this copy in `ValidateExact` mode and verify readiness.
4. On a disposable copy only, add two valid Draft rows with distinct `Version` values and `IsCurrent=false`, then verify that the second migration fails with a unique violation, does not record `20260923145307_AddDesignTokenDraftUniqueness`, does not remove either row, and leaves the first migration recorded. This proves the required stop path. For the success rerun, change only these synthetic rows to `Archived`, rerun the same script and verify that both rows remain. Never use this synthetic status change on production.

   Example rows for that disposable-only conflict test (use IDs confirmed absent from the copy):

   ```sql
   INSERT INTO "DesignTokenVersion"
       ("Id", "Version", "Status", "BaseWidth", "IsCurrent", "CreatedAtUtc", "UpdatedAtUtc")
   VALUES
       ('00000000-0000-4000-8000-000000000001', 'migration-test-a-20261022', 'Draft', 390, FALSE, now(), now()),
       ('00000000-0000-4000-8000-000000000002', 'migration-test-b-20261022', 'Draft', 390, FALSE, now(), now());

   UPDATE "DesignTokenVersion"
   SET "Status" = 'Archived'
   WHERE "Id" IN (
       '00000000-0000-4000-8000-000000000001',
       '00000000-0000-4000-8000-000000000002');
   ```
5. For production, repeat the read-only history, version, table-size and duplicate-Draft preflight. If the history already reaches the target and exact readiness is healthy, stop with no change. If the database is at the expected baseline and the data checks pass, take and verify a fresh backup, then obtain explicit production migration authorization.
6. During the approved low-traffic window, apply only the reviewed script. Use a short `lock_timeout` selected by the operator; abort and reschedule if locks cannot be acquired. Set a statement timeout based on the rehearsed table sizes and observed index build duration. Do not replace these operations with `CREATE INDEX CONCURRENTLY` inside this transactional script.
7. Verify the migration history, catalog definitions, Draft count, and unchanged row counts using the checks below. Then deploy through the manual Fly `Deploy to Fly.io` workflow with `confirmDatabaseMigrated=true`; startup validation stays enabled and automatic production migrations stay disabled.

Example command for the separately authorized production step (not executed in this audit):

```powershell
@'
SET lock_timeout = '5s';
SET statement_timeout = '15min';
\i 'scripts/db/generated/api-20260908130000-to-20260923145307-idempotent.sql'
'@ | psql --dbname=$db --no-psqlrc --set=ON_ERROR_STOP=1 --file=-
if ($LASTEXITCODE -ne 0) { throw 'Migration failed. Stop; inspect migration history and schema before retrying.' }
```

The `15min` statement timeout is a starting value only; replace it with the value validated by the rehearsal and production table sizes. The `5s` lock timeout makes the operation fail quickly when a conflicting transaction prevents a lock. Do not run this command until all preflight, backup, rehearsal and explicit authorization gates pass.

### Post-migration checks

```powershell
psql --dbname=$db --no-psqlrc --set=ON_ERROR_STOP=1 --command='SELECT "MigrationId" FROM "__EFMigrationsHistory" WHERE "MigrationId" IN (''20260919104120_AddIdempotencyLeases'',''20260923145307_AddDesignTokenDraftUniqueness'') ORDER BY "MigrationId";'
psql --dbname=$db --no-psqlrc --set=ON_ERROR_STOP=1 --command='SELECT count(*) FROM "DesignTokenVersion" WHERE "Status" = ''Draft'';'
psql --dbname=$db --no-psqlrc --set=ON_ERROR_STOP=1 --command="SELECT table_name, column_name, data_type, is_nullable, column_default FROM information_schema.columns WHERE (table_name, column_name) IN (('economy_transactions','AttemptCount'),('economy_transactions','LeaseExpiresAtUtc'),('economy_transactions','OwnerToken'),('cosmetics_idempotency_ledger','AttemptCount'),('cosmetics_idempotency_ledger','LeaseExpiresAtUtc'),('cosmetics_idempotency_ledger','OwnerToken')) ORDER BY table_name, column_name;"
psql --dbname=$db --no-psqlrc --set=ON_ERROR_STOP=1 --command="SELECT c.relname AS index_name, i.indisvalid, i.indisunique, pg_get_indexdef(i.indexrelid) AS index_definition FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE c.relname IN ('IX_SyncEventLog_Status_ReceivedAtUtc_Id','IX_SyncDeadLetter_Status_LastFailedAtUtc_Id','IX_ServerSyncEvent_CreatedAtUtc_Id','IX_economy_transactions_pending_lease','IX_cosmetics_idempotency_ledger_pending_lease','UX_DesignTokenVersion_Draft') ORDER BY c.relname;"
```

Require both migration IDs exactly once, six new columns with the expected nullability/defaults, all six indexes present and `indisvalid=true`, Draft count at most one, and row counts unchanged from the pre-migration capture. In the rehearsal, pre-existing rows should read `AttemptCount=0` and null lease/owner fields before the API starts; production values may legitimately change after the target API starts processing leases. Then deploy the target API and verify `/api/health/ready` returns success and startup logs report `ValidateExact` with no pending or unknown migrations.

### Failure and recovery

- If the first migration fails, stop. Its transaction should roll back. Confirm neither its history row nor partial DDL remains before retrying; do not edit migration history manually.
- If the first migration succeeds and the Draft unique index fails, the first migration remains committed and the second migration transaction rolls back. Keep all Draft records. Stop, have the data owner decide a valid lifecycle status for duplicates, rehearse that data-preserving change on a copy, and obtain separate approval before any production data update. Re-run the same idempotent script only after the duplicate count is at most one.
- If the API deployment fails after the schema is updated, keep schema validation enabled. Deploy a corrected API build whose migration assembly includes both applied migrations. Only roll back to an older image after proving that its migration assembly recognizes the database history; otherwise `ValidateExact` will reject it.
- Do not run `Down` migrations. If database-level recovery is needed, restore to a new Neon branch/isolated target from the verified pre-migration backup or point in time, validate data and migration history there, and plan an approved cutover with post-backup writes reconciled. Never reset or recreate the live production database.
- This audit did not execute a production query, backup, restore, migration or deployment. Production migration execution remains a separate explicitly approved step.

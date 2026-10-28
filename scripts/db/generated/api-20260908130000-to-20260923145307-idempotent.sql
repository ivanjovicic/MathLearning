START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919104120_AddIdempotencyLeases') THEN
    ALTER TABLE economy_transactions ADD "AttemptCount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919104120_AddIdempotencyLeases') THEN
    ALTER TABLE economy_transactions ADD "LeaseExpiresAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919104120_AddIdempotencyLeases') THEN
    ALTER TABLE economy_transactions ADD "OwnerToken" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919104120_AddIdempotencyLeases') THEN
    ALTER TABLE cosmetics_idempotency_ledger ADD "AttemptCount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919104120_AddIdempotencyLeases') THEN
    ALTER TABLE cosmetics_idempotency_ledger ADD "LeaseExpiresAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919104120_AddIdempotencyLeases') THEN
    ALTER TABLE cosmetics_idempotency_ledger ADD "OwnerToken" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919104120_AddIdempotencyLeases') THEN
    CREATE INDEX "IX_SyncEventLog_Status_ReceivedAtUtc_Id" ON "SyncEventLog" ("Status", "ReceivedAtUtc", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919104120_AddIdempotencyLeases') THEN
    CREATE INDEX "IX_SyncDeadLetter_Status_LastFailedAtUtc_Id" ON "SyncDeadLetter" ("Status", "LastFailedAtUtc", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919104120_AddIdempotencyLeases') THEN
    CREATE INDEX "IX_ServerSyncEvent_CreatedAtUtc_Id" ON "ServerSyncEvent" ("CreatedAtUtc", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919104120_AddIdempotencyLeases') THEN
    CREATE INDEX "IX_economy_transactions_pending_lease" ON economy_transactions ("Status", "LeaseExpiresAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919104120_AddIdempotencyLeases') THEN
    CREATE INDEX "IX_cosmetics_idempotency_ledger_pending_lease" ON cosmetics_idempotency_ledger ("Status", "LeaseExpiresAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919104120_AddIdempotencyLeases') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260919104120_AddIdempotencyLeases', '8.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923145307_AddDesignTokenDraftUniqueness') THEN
    CREATE UNIQUE INDEX "UX_DesignTokenVersion_Draft" ON "DesignTokenVersion" ("Status") WHERE "Status" = 'Draft';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923145307_AddDesignTokenDraftUniqueness') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260923145307_AddDesignTokenDraftUniqueness', '8.0.12');
    END IF;
END $EF$;
COMMIT;

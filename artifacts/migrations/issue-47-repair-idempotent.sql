START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101185317_RepairUserAnswerAuditXpSchema') THEN
    SET LOCAL lock_timeout = '5s';
    SET LOCAL statement_timeout = '60s';

    DO $repair$
    DECLARE
        expected RECORD;
        actual RECORD;
        dup_count bigint;
        idx_def text;
    BEGIN
        IF to_regclass('public."UserAnswerAudits"') IS NULL THEN
            RAISE EXCEPTION 'Issue47 repair: table public."UserAnswerAudits" does not exist';
        END IF;

        -- 1. Validate any already-present columns have the expected type (no silent type drift).
        FOR expected IN
            SELECT * FROM (VALUES
                ('UserId', 'text', NULL::int),
                ('QuestionId', 'integer', NULL::int),
                ('ClientId', 'character varying', 128),
                ('CreatedAt', 'timestamp with time zone', NULL::int),
                ('IsFirstTimeCorrect', 'boolean', NULL::int),
                ('IsOffline', 'boolean', NULL::int),
                ('Reason', 'character varying', 64),
                ('Source', 'character varying', 50),
                ('TotalXpAfterAward', 'integer', NULL::int)
            ) AS t(col, data_type, max_len)
        LOOP
            SELECT c.data_type, c.character_maximum_length INTO actual
            FROM information_schema.columns c
            WHERE c.table_schema = 'public' AND c.table_name = 'UserAnswerAudits' AND c.column_name = expected.col;

            IF FOUND AND (actual.data_type <> expected.data_type
                OR (expected.max_len IS NOT NULL AND actual.character_maximum_length IS DISTINCT FROM expected.max_len)) THEN
                RAISE EXCEPTION 'Issue47 repair: column UserAnswerAudits.% has type %(%), expected %(%)',
                    expected.col, actual.data_type, actual.character_maximum_length, expected.data_type, expected.max_len;
            END IF;
        END LOOP;

        -- 2. Validate pre-existing unique index (if any) has the expected definition.
        SELECT indexdef INTO idx_def FROM pg_indexes
        WHERE schemaname = 'public' AND indexname = 'UX_UserAnswerAudits_FirstCorrect_PerQuestion';
        IF idx_def IS NOT NULL AND (idx_def NOT ILIKE 'CREATE UNIQUE INDEX%'
            OR idx_def NOT ILIKE '%("UserId", "QuestionId", "IsFirstTimeCorrect")%'
            OR idx_def NOT ILIKE '%WHERE%IsFirstTimeCorrect%true%') THEN
            RAISE EXCEPTION 'Issue47 repair: existing UX_UserAnswerAudits_FirstCorrect_PerQuestion has unexpected definition: %', idx_def;
        END IF;

        -- 3. Additive columns (defaults are constants / NOW(): no table rewrite on PG11+).
        ALTER TABLE public."UserAnswerAudits"
            ADD COLUMN IF NOT EXISTS "ClientId" character varying(128) NULL,
            ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
            ADD COLUMN IF NOT EXISTS "IsFirstTimeCorrect" boolean NOT NULL DEFAULT FALSE,
            ADD COLUMN IF NOT EXISTS "IsOffline" boolean NOT NULL DEFAULT FALSE,
            ADD COLUMN IF NOT EXISTS "Reason" character varying(64) NOT NULL DEFAULT 'not_eligible',
            ADD COLUMN IF NOT EXISTS "Source" character varying(50) NOT NULL DEFAULT 'quiz_answer',
            ADD COLUMN IF NOT EXISTS "TotalXpAfterAward" integer NOT NULL DEFAULT 0;

        -- 4. Duplicate guard before the filtered unique index.
        SELECT count(*) INTO dup_count FROM (
            SELECT 1 FROM public."UserAnswerAudits"
            WHERE "IsFirstTimeCorrect" = TRUE
            GROUP BY "UserId", "QuestionId"
            HAVING count(*) > 1) d;
        IF dup_count > 0 THEN
            RAISE EXCEPTION 'Issue47 repair: % duplicate first-correct (UserId, QuestionId) groups; resolve before creating UX index', dup_count;
        END IF;
    END
    $repair$;

    CREATE INDEX IF NOT EXISTS "IX_UserAnswerAudits_UserId" ON public."UserAnswerAudits" ("UserId");
    CREATE INDEX IF NOT EXISTS "IX_UserAnswerAudits_QuestionId" ON public."UserAnswerAudits" ("QuestionId");
    CREATE INDEX IF NOT EXISTS "IX_UserAnswerAudits_CreatedAt" ON public."UserAnswerAudits" ("CreatedAt");
    CREATE INDEX IF NOT EXISTS "IX_UserAnswerAudits_User_Question_CreatedAt" ON public."UserAnswerAudits" ("UserId", "QuestionId", "CreatedAt");
    CREATE UNIQUE INDEX IF NOT EXISTS "UX_UserAnswerAudits_FirstCorrect_PerQuestion" ON public."UserAnswerAudits" ("UserId", "QuestionId", "IsFirstTimeCorrect") WHERE "IsFirstTimeCorrect" = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101185317_RepairUserAnswerAuditXpSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101185317_RepairUserAnswerAuditXpSchema', '8.0.32');
    END IF;
END $EF$;
COMMIT;


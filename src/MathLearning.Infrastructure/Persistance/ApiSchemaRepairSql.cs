namespace MathLearning.Infrastructure.Persistance;

public static class ApiSchemaRepairSql
{
    public const string CriticalRepair = """
        ALTER TABLE IF EXISTS "SyncDeadLetter"
        ADD COLUMN IF NOT EXISTS "LastRedriveAttemptAtUtc" timestamp with time zone NULL;

        ALTER TABLE IF EXISTS "SyncDeadLetter"
        ADD COLUMN IF NOT EXISTS "ResolutionNote" character varying(2048) NULL;

        ALTER TABLE IF EXISTS "SyncDeadLetter"
        ADD COLUMN IF NOT EXISTS "ResolvedAtUtc" timestamp with time zone NULL;

        ALTER TABLE IF EXISTS "SyncDeadLetter"
        ADD COLUMN IF NOT EXISTS "LastFailedAtUtc" timestamp with time zone NULL;

        ALTER TABLE IF EXISTS "SyncDeadLetter"
        ADD COLUMN IF NOT EXISTS "Status" character varying(32) NOT NULL DEFAULT 'Pending';

        ALTER TABLE IF EXISTS "SyncDeadLetter"
        ADD COLUMN IF NOT EXISTS "RetryCount" integer NOT NULL DEFAULT 0;

        ALTER TABLE IF EXISTS "SyncDeadLetter"
        ADD COLUMN IF NOT EXISTS "SyncEventLogId" integer NULL;

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "CurrentDraftId" uuid NULL;

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "CurrentVersionNumber" integer NOT NULL DEFAULT 0;

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "HintFull" text NULL;

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "PublishState" character varying(32) NOT NULL DEFAULT 'draft';

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "PublishedAtUtc" timestamp with time zone NULL;

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "PublishedByUserId" character varying(450) NULL;

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "PreviousSnapshotJson" jsonb NULL;

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "UpdatedBy" character varying(256) NULL;

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "ExplanationFormat" character varying(32) NOT NULL DEFAULT 'MarkdownWithMath';

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "ExplanationRenderMode" character varying(32) NOT NULL DEFAULT 'Auto';

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "HintFormat" character varying(32) NOT NULL DEFAULT 'MarkdownWithMath';

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "HintRenderMode" character varying(32) NOT NULL DEFAULT 'Auto';

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "SemanticsAltText" character varying(1000) NULL;

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "TextFormat" character varying(32) NOT NULL DEFAULT 'MarkdownWithMath';

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "TextRenderMode" character varying(32) NOT NULL DEFAULT 'Auto';

        ALTER TABLE IF EXISTS "Questions"
        ADD COLUMN IF NOT EXISTS "CorrectOptionId" integer NULL;

        ALTER TABLE IF EXISTS "QuestionSteps"
        ADD COLUMN IF NOT EXISTS "HintFormat" character varying(32) NOT NULL DEFAULT 'MarkdownWithMath';

        ALTER TABLE IF EXISTS "QuestionSteps"
        ADD COLUMN IF NOT EXISTS "HintRenderMode" character varying(32) NOT NULL DEFAULT 'Auto';

        ALTER TABLE IF EXISTS "QuestionSteps"
        ADD COLUMN IF NOT EXISTS "SemanticsAltText" character varying(500) NULL;

        ALTER TABLE IF EXISTS "QuestionSteps"
        ADD COLUMN IF NOT EXISTS "TextFormat" character varying(32) NOT NULL DEFAULT 'MarkdownWithMath';

        ALTER TABLE IF EXISTS "QuestionSteps"
        ADD COLUMN IF NOT EXISTS "TextRenderMode" character varying(32) NOT NULL DEFAULT 'Auto';

        ALTER TABLE IF EXISTS "Options"
        ADD COLUMN IF NOT EXISTS "RenderMode" character varying(32) NOT NULL DEFAULT 'Auto';

        ALTER TABLE IF EXISTS "Options"
        ADD COLUMN IF NOT EXISTS "SemanticsAltText" character varying(500) NULL;

        ALTER TABLE IF EXISTS "Options"
        ADD COLUMN IF NOT EXISTS "TextFormat" character varying(32) NOT NULL DEFAULT 'MarkdownWithMath';

        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'SyncDeadLetter'
            ) THEN
                CREATE INDEX IF NOT EXISTS "IX_SyncDeadLetter_Status_LastFailedAtUtc"
                    ON "SyncDeadLetter" ("Status", "LastFailedAtUtc");
            END IF;
        END $$;

        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'Questions'
            ) THEN
                CREATE INDEX IF NOT EXISTS "IX_Questions_CurrentDraftId"
                    ON "Questions" ("CurrentDraftId");

                CREATE INDEX IF NOT EXISTS "IX_Questions_PublishState"
                    ON "Questions" ("PublishState");

                CREATE INDEX IF NOT EXISTS "IX_Questions_CorrectOptionId"
                    ON "Questions" ("CorrectOptionId");
            END IF;
        END $$;

        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'Questions'
            ) AND EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'Options'
            ) AND NOT EXISTS (
                SELECT 1
                FROM information_schema.table_constraints
                WHERE constraint_schema = 'public'
                  AND table_name = 'Questions'
                  AND constraint_name = 'FK_Questions_Options_CorrectOptionId'
            ) THEN
                ALTER TABLE "Questions"
                ADD CONSTRAINT "FK_Questions_Options_CorrectOptionId"
                FOREIGN KEY ("CorrectOptionId") REFERENCES "Options" ("Id") ON DELETE SET NULL;
            END IF;
        END $$;
        """;
}
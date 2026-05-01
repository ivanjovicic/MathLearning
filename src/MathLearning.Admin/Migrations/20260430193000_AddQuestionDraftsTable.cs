using MathLearning.Admin.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Admin.Migrations
{
    [DbContext(typeof(AdminDbContext))]
    [Migration("20260430193000_AddQuestionDraftsTable")]
    public partial class AddQuestionDraftsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS "question_drafts" (
    "Id" uuid NOT NULL,
    "QuestionId" integer NULL,
    "PreviousDraftId" uuid NULL,
    "DraftVersion" integer NOT NULL DEFAULT 1,
    "ContentJson" text NOT NULL DEFAULT '{}',
    "NormalizedContentJson" text NOT NULL DEFAULT '{}',
    "ContentHash" text NOT NULL DEFAULT '',
    "PublishState" text NOT NULL DEFAULT 'draft',
    "ValidationStatus" text NOT NULL DEFAULT 'pending',
    "ChangeReason" text NULL,
    "AuthorUserId" text NULL,
    "EditorUserId" text NULL,
    "LatestValidationResultId" uuid NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAtUtc" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_question_drafts" PRIMARY KEY ("Id")
);
""");

            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_question_drafts_QuestionId" ON "question_drafts" ("QuestionId");""");
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_question_drafts_PreviousDraftId" ON "question_drafts" ("PreviousDraftId");""");
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_question_drafts_ContentHash" ON "question_drafts" ("ContentHash");""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF to_regclass('public."Questions"') IS NULL THEN
        RETURN;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_question_drafts_Questions_QuestionId'
    ) THEN
        ALTER TABLE "question_drafts"
            ADD CONSTRAINT "FK_question_drafts_Questions_QuestionId"
            FOREIGN KEY ("QuestionId") REFERENCES "Questions" ("Id") ON DELETE SET NULL;
    END IF;
END
$$;
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_question_drafts_question_drafts_PreviousDraftId'
    ) THEN
        ALTER TABLE "question_drafts"
            ADD CONSTRAINT "FK_question_drafts_question_drafts_PreviousDraftId"
            FOREIGN KEY ("PreviousDraftId") REFERENCES "question_drafts" ("Id") ON DELETE SET NULL;
    END IF;
END
$$;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non-destructive: this table may be shared with API/admin authoring data.
        }
    }
}

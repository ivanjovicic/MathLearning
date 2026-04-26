using MathLearning.Admin.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Admin.Migrations
{
    [DbContext(typeof(AdminDbContext))]
    [Migration("20260426120000_AddContentFormatToQuestions")]
    public partial class AddContentFormatToQuestions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddColumnIfMissing(migrationBuilder, "Questions", "TextFormat", "character varying(32) NOT NULL DEFAULT 'MarkdownWithMath'");
            AddColumnIfMissing(migrationBuilder, "Questions", "ExplanationFormat", "character varying(32) NOT NULL DEFAULT 'MarkdownWithMath'");
            AddColumnIfMissing(migrationBuilder, "Questions", "HintFormat", "character varying(32) NOT NULL DEFAULT 'MarkdownWithMath'");
            AddColumnIfMissing(migrationBuilder, "Options", "TextFormat", "character varying(32) NOT NULL DEFAULT 'MarkdownWithMath'");
            AddColumnIfMissing(migrationBuilder, "QuestionSteps", "TextFormat", "character varying(32) NOT NULL DEFAULT 'MarkdownWithMath'");
            AddColumnIfMissing(migrationBuilder, "QuestionSteps", "HintFormat", "character varying(32) NOT NULL DEFAULT 'MarkdownWithMath'");

            NormalizeContentFormatColumn(migrationBuilder, "Questions", "TextFormat");
            NormalizeContentFormatColumn(migrationBuilder, "Questions", "ExplanationFormat");
            NormalizeContentFormatColumn(migrationBuilder, "Questions", "HintFormat");
            NormalizeContentFormatColumn(migrationBuilder, "Options", "TextFormat");
            NormalizeContentFormatColumn(migrationBuilder, "QuestionSteps", "TextFormat");
            NormalizeContentFormatColumn(migrationBuilder, "QuestionSteps", "HintFormat");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally non-destructive: this migration hardens content-format metadata
            // that may already exist from earlier admin schema repair migrations.
        }

        private static void AddColumnIfMissing(MigrationBuilder migrationBuilder, string table, string column, string definition)
            => migrationBuilder.Sql($@"ALTER TABLE IF EXISTS ""{table}"" ADD COLUMN IF NOT EXISTS ""{column}"" {definition};");

        private static void NormalizeContentFormatColumn(MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql($@"
DO $$
BEGIN
    IF to_regclass('public.""{table}""') IS NULL THEN
        RETURN;
    END IF;

    EXECUTE $sql$UPDATE ""{table}"" SET ""{column}"" = 'MarkdownWithMath' WHERE ""{column}"" IS NULL OR btrim(""{column}"") = ''$sql$;
    EXECUTE $sql$UPDATE ""{table}"" SET ""{column}"" = 'LaTeX' WHERE ""{column}"" = 'Latex'$sql$;
    EXECUTE $sql$ALTER TABLE ""{table}"" ALTER COLUMN ""{column}"" SET DEFAULT 'MarkdownWithMath'$sql$;
END
$$;
");
        }
    }
}

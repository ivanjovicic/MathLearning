using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddMathContentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "QuestionSteps"
                ADD COLUMN IF NOT EXISTS "HintFormat" character varying(32) NOT NULL DEFAULT 'MarkdownWithMath';

                ALTER TABLE "QuestionSteps"
                ADD COLUMN IF NOT EXISTS "HintRenderMode" character varying(32) NOT NULL DEFAULT 'Auto';

                ALTER TABLE "QuestionSteps"
                ADD COLUMN IF NOT EXISTS "SemanticsAltText" character varying(500) NULL;

                ALTER TABLE "QuestionSteps"
                ADD COLUMN IF NOT EXISTS "TextFormat" character varying(32) NOT NULL DEFAULT 'MarkdownWithMath';

                ALTER TABLE "QuestionSteps"
                ADD COLUMN IF NOT EXISTS "TextRenderMode" character varying(32) NOT NULL DEFAULT 'Auto';

                ALTER TABLE "Questions"
                ADD COLUMN IF NOT EXISTS "ExplanationFormat" character varying(32) NOT NULL DEFAULT 'MarkdownWithMath';

                ALTER TABLE "Questions"
                ADD COLUMN IF NOT EXISTS "ExplanationRenderMode" character varying(32) NOT NULL DEFAULT 'Auto';

                ALTER TABLE "Questions"
                ADD COLUMN IF NOT EXISTS "HintFormat" character varying(32) NOT NULL DEFAULT 'MarkdownWithMath';

                ALTER TABLE "Questions"
                ADD COLUMN IF NOT EXISTS "HintRenderMode" character varying(32) NOT NULL DEFAULT 'Auto';

                ALTER TABLE "Questions"
                ADD COLUMN IF NOT EXISTS "SemanticsAltText" character varying(1000) NULL;

                ALTER TABLE "Questions"
                ADD COLUMN IF NOT EXISTS "TextFormat" character varying(32) NOT NULL DEFAULT 'MarkdownWithMath';

                ALTER TABLE "Questions"
                ADD COLUMN IF NOT EXISTS "TextRenderMode" character varying(32) NOT NULL DEFAULT 'Auto';

                ALTER TABLE "Options"
                ADD COLUMN IF NOT EXISTS "RenderMode" character varying(32) NOT NULL DEFAULT 'Auto';

                ALTER TABLE "Options"
                ADD COLUMN IF NOT EXISTS "SemanticsAltText" character varying(500) NULL;

                ALTER TABLE "Options"
                ADD COLUMN IF NOT EXISTS "TextFormat" character varying(32) NOT NULL DEFAULT 'MarkdownWithMath';
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HintFormat",
                table: "QuestionSteps");

            migrationBuilder.DropColumn(
                name: "HintRenderMode",
                table: "QuestionSteps");

            migrationBuilder.DropColumn(
                name: "SemanticsAltText",
                table: "QuestionSteps");

            migrationBuilder.DropColumn(
                name: "TextFormat",
                table: "QuestionSteps");

            migrationBuilder.DropColumn(
                name: "TextRenderMode",
                table: "QuestionSteps");

            migrationBuilder.DropColumn(
                name: "ExplanationFormat",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "ExplanationRenderMode",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "HintFormat",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "HintRenderMode",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "SemanticsAltText",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TextFormat",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TextRenderMode",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "RenderMode",
                table: "Options");

            migrationBuilder.DropColumn(
                name: "SemanticsAltText",
                table: "Options");

            migrationBuilder.DropColumn(
                name: "TextFormat",
                table: "Options");
        }
    }
}

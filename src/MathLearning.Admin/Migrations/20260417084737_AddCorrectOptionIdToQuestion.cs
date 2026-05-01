using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrectOptionIdToQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE IF EXISTS "Questions" ADD COLUMN IF NOT EXISTS "CorrectOptionId" integer;""");

            migrationBuilder.Sql("""
UPDATE "Questions" q
SET "CorrectOptionId" = (
    SELECT o."Id"
    FROM "Options" o
    WHERE o."QuestionId" = q."Id"
      AND q."CorrectAnswer" IS NOT NULL
      AND o."Text" = q."CorrectAnswer"
    ORDER BY o."Id"
    LIMIT 1
)
WHERE q."CorrectOptionId" IS NULL
  AND q."CorrectAnswer" IS NOT NULL
  AND EXISTS (
      SELECT 1
      FROM "Options" o
      WHERE o."QuestionId" = q."Id"
        AND o."Text" = q."CorrectAnswer"
  );
""");

            migrationBuilder.Sql("""
UPDATE "Questions" q
SET "CorrectOptionId" = (
    SELECT o."Id"
    FROM "Options" o
    WHERE o."QuestionId" = q."Id"
      AND o."IsCorrect" = TRUE
    ORDER BY o."Id"
    LIMIT 1
)
WHERE q."CorrectOptionId" IS NULL
  AND EXISTS (
      SELECT 1
      FROM "Options" o
      WHERE o."QuestionId" = q."Id"
        AND o."IsCorrect" = TRUE
  );
""");

            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_Questions_CorrectOptionId" ON "Questions" ("CorrectOptionId");""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF to_regclass('public."Questions"') IS NULL OR to_regclass('public."Options"') IS NULL THEN
        RETURN;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_Questions_Options_CorrectOptionId'
    ) THEN
        ALTER TABLE "Questions"
            ADD CONSTRAINT "FK_Questions_Options_CorrectOptionId"
            FOREIGN KEY ("CorrectOptionId") REFERENCES "Options" ("Id") ON DELETE SET NULL;
    END IF;
END
$$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Options_CorrectOptionId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_CorrectOptionId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "CorrectOptionId",
                table: "Questions");
        }
    }
}

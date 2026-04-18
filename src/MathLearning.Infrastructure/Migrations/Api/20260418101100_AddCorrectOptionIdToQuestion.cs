using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api;

[DbContext(typeof(ApiDbContext))]
[Migration("20260418101100_AddCorrectOptionIdToQuestion")]
public partial class AddCorrectOptionIdToQuestion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
ALTER TABLE "Questions"
ADD COLUMN IF NOT EXISTS "CorrectOptionId" integer NULL;
""");

        migrationBuilder.Sql("""
UPDATE "Questions" q
SET "CorrectOptionId" = matched."Id"
FROM LATERAL (
    SELECT o."Id"
    FROM "Options" o
    WHERE o."QuestionId" = q."Id"
      AND q."CorrectAnswer" IS NOT NULL
      AND o."Text" = q."CorrectAnswer"
    ORDER BY o."Id"
    LIMIT 1
) matched
WHERE q."CorrectOptionId" IS NULL;
""");

        migrationBuilder.Sql("""
UPDATE "Questions" q
SET "CorrectOptionId" = matched."Id"
FROM LATERAL (
    SELECT o."Id"
    FROM "Options" o
    WHERE o."QuestionId" = q."Id"
      AND o."IsCorrect" = TRUE
    ORDER BY o."Id"
    LIMIT 1
) matched
WHERE q."CorrectOptionId" IS NULL;
""");

        migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_Questions_CorrectOptionId" ON "Questions" ("CorrectOptionId");
""");

        migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_Questions_Options_CorrectOptionId'
    ) THEN
        ALTER TABLE "Questions"
        ADD CONSTRAINT "FK_Questions_Options_CorrectOptionId"
        FOREIGN KEY ("CorrectOptionId") REFERENCES "Options" ("Id") ON DELETE SET NULL;
    END IF;
END $$;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
ALTER TABLE "Questions"
DROP CONSTRAINT IF EXISTS "FK_Questions_Options_CorrectOptionId";
""");

        migrationBuilder.Sql("""
DROP INDEX IF EXISTS "IX_Questions_CorrectOptionId";
""");

        migrationBuilder.Sql("""
ALTER TABLE "Questions"
DROP COLUMN IF EXISTS "CorrectOptionId";
""");
    }
}

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
            migrationBuilder.AddColumn<int>(
                name: "CorrectOptionId",
                table: "Questions",
                type: "integer",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Questions_CorrectOptionId",
                table: "Questions",
                column: "CorrectOptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Options_CorrectOptionId",
                table: "Questions",
                column: "CorrectOptionId",
                principalTable: "Options",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
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

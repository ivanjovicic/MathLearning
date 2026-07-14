using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddQuizAttemptIngestAttemptKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttemptKey",
                table: "quiz_attempt",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "UX_quiz_attempt_attempt_key",
                table: "quiz_attempt",
                column: "AttemptKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_quiz_attempt_attempt_key",
                table: "quiz_attempt");

            migrationBuilder.DropColumn(
                name: "AttemptKey",
                table: "quiz_attempt");
        }
    }
}

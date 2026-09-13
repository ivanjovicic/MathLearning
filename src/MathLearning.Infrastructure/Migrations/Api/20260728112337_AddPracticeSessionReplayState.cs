using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddPracticeSessionReplayState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SettledResponseJson",
                table: "practice_session_item",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionFingerprintJson",
                table: "practice_session_item",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionResponseJson",
                table: "practice_session",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SettledResponseJson",
                table: "practice_session_item");

            migrationBuilder.DropColumn(
                name: "SubmissionFingerprintJson",
                table: "practice_session_item");

            migrationBuilder.DropColumn(
                name: "CompletionResponseJson",
                table: "practice_session");
        }
    }
}

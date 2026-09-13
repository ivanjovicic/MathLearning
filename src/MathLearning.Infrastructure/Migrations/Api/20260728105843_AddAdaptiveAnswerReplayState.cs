using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddAdaptiveAnswerReplayState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprintJson",
                table: "user_question_history",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettledResponseJson",
                table: "user_question_history",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_UserQuestionHistory_AdaptiveSessionItem",
                table: "user_question_history",
                column: "AdaptiveSessionItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_UserQuestionHistory_AdaptiveSessionItem",
                table: "user_question_history");

            migrationBuilder.DropColumn(
                name: "RequestFingerprintJson",
                table: "user_question_history");

            migrationBuilder.DropColumn(
                name: "SettledResponseJson",
                table: "user_question_history");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class HardenXpAuditPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "UserAnswerAudits",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserAnswerAudits",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<bool>(
                name: "IsFirstTimeCorrect",
                table: "UserAnswerAudits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOffline",
                table: "UserAnswerAudits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "UserAnswerAudits",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "not_eligible");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "UserAnswerAudits",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "quiz_answer");

            migrationBuilder.AddColumn<int>(
                name: "TotalXpAfterAward",
                table: "UserAnswerAudits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_UserAnswerAudits_CreatedAt",
                table: "UserAnswerAudits",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserAnswerAudits_User_Question_CreatedAt",
                table: "UserAnswerAudits",
                columns: new[] { "UserId", "QuestionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_UserAnswerAudits_FirstCorrect_PerQuestion",
                table: "UserAnswerAudits",
                columns: new[] { "UserId", "QuestionId", "IsFirstTimeCorrect" },
                unique: true,
                filter: "\"IsFirstTimeCorrect\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserAnswerAudits_CreatedAt",
                table: "UserAnswerAudits");

            migrationBuilder.DropIndex(
                name: "IX_UserAnswerAudits_User_Question_CreatedAt",
                table: "UserAnswerAudits");

            migrationBuilder.DropIndex(
                name: "UX_UserAnswerAudits_FirstCorrect_PerQuestion",
                table: "UserAnswerAudits");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "UserAnswerAudits");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserAnswerAudits");

            migrationBuilder.DropColumn(
                name: "IsFirstTimeCorrect",
                table: "UserAnswerAudits");

            migrationBuilder.DropColumn(
                name: "IsOffline",
                table: "UserAnswerAudits");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "UserAnswerAudits");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "UserAnswerAudits");

            migrationBuilder.DropColumn(
                name: "TotalXpAfterAward",
                table: "UserAnswerAudits");
        }
    }
}

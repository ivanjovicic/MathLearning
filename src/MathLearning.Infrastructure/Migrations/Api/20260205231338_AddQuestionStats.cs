using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddQuestionStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "question_stats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    LastAnswered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuccessStreak = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Ease = table.Column<double>(type: "double precision", nullable: false, defaultValue: 1.3),
                    NextReview = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_stats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_stats_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_question_stats_QuestionId",
                table: "question_stats",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionStats_User_NextReview",
                table: "question_stats",
                columns: new[] { "UserId", "NextReview" });

            migrationBuilder.CreateIndex(
                name: "UX_QuestionStats_User_Question",
                table: "question_stats",
                columns: new[] { "UserId", "QuestionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "question_stats");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    public partial class AddPracticeSessionPipeline : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mastery_state",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    SubtopicId = table.Column<int>(type: "integer", nullable: true),
                    PL = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mastery_state", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "practice_session",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: true),
                    SubtopicId = table.Column<int>(type: "integer", nullable: true),
                    SkillNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    TargetQuestions = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    AnsweredQuestions = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CorrectAnswers = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    XpEarned = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RecommendedDifficulty = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "medium"),
                    InitialMastery = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    FinalMastery = table.Column<decimal>(type: "numeric(5,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_session", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "practice_session_item",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    SubtopicId = table.Column<int>(type: "integer", nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "medium"),
                    PresentedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Correct = table.Column<bool>(type: "boolean", nullable: true),
                    TimeSpentMs = table.Column<int>(type: "integer", nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    BktPrior = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    BktPosterior = table.Column<decimal>(type: "numeric(5,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_session_item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_practice_session_item_practice_session_SessionId",
                        column: x => x.SessionId,
                        principalTable: "practice_session",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mastery_state_user_updated_at",
                table: "mastery_state",
                columns: new[] { "UserId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_mastery_state_user_topic_subtopic",
                table: "mastery_state",
                columns: new[] { "UserId", "TopicId", "SubtopicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_practice_session_user_started_at",
                table: "practice_session",
                columns: new[] { "UserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_practice_session_user_status",
                table: "practice_session",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_practice_session_user_topic",
                table: "practice_session",
                columns: new[] { "UserId", "TopicId" });

            migrationBuilder.CreateIndex(
                name: "IX_practice_session_user_subtopic",
                table: "practice_session",
                columns: new[] { "UserId", "SubtopicId" });

            migrationBuilder.CreateIndex(
                name: "IX_practice_session_item_question_id",
                table: "practice_session_item",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_practice_session_item_session_id",
                table: "practice_session_item",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_practice_session_item_session_presented_at",
                table: "practice_session_item",
                columns: new[] { "SessionId", "PresentedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_practice_session_item_session_question_attempt",
                table: "practice_session_item",
                columns: new[] { "SessionId", "QuestionId", "AttemptNumber" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "mastery_state");
            migrationBuilder.DropTable(name: "practice_session_item");
            migrationBuilder.DropTable(name: "practice_session");
        }
    }
}

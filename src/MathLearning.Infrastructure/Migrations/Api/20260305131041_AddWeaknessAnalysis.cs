using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddWeaknessAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quiz_attempt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuizId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    SubtopicId = table.Column<int>(type: "integer", nullable: false),
                    Correct = table.Column<bool>(type: "boolean", nullable: false),
                    TimeSpentMs = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_attempt", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_subtopic_stats",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubtopicId = table.Column<int>(type: "integer", nullable: false),
                    TotalQuestions = table.Column<int>(type: "integer", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "integer", nullable: false),
                    Accuracy = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    LastAttempt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WeaknessScore = table.Column<decimal>(type: "numeric(8,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_subtopic_stats", x => new { x.UserId, x.SubtopicId });
                });

            migrationBuilder.CreateTable(
                name: "user_topic_stats",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    TotalQuestions = table.Column<int>(type: "integer", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "integer", nullable: false),
                    Accuracy = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    LastAttempt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WeaknessScore = table.Column<decimal>(type: "numeric(8,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_topic_stats", x => new { x.UserId, x.TopicId });
                });

            migrationBuilder.CreateTable(
                name: "user_weakness",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: true),
                    SubtopicId = table.Column<int>(type: "integer", nullable: true),
                    WeaknessLevel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    RecommendedPractice = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_weakness", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quiz_attempt_user_correct",
                table: "quiz_attempt",
                columns: new[] { "UserId", "Correct" });

            migrationBuilder.CreateIndex(
                name: "IX_quiz_attempt_user_created_at",
                table: "quiz_attempt",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_quiz_attempt_user_subtopic_created_at",
                table: "quiz_attempt",
                columns: new[] { "UserId", "SubtopicId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_quiz_attempt_user_topic_created_at",
                table: "quiz_attempt",
                columns: new[] { "UserId", "TopicId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_subtopic_stats_user_accuracy",
                table: "user_subtopic_stats",
                columns: new[] { "UserId", "Accuracy" });

            migrationBuilder.CreateIndex(
                name: "IX_user_subtopic_stats_user_weakness_score",
                table: "user_subtopic_stats",
                columns: new[] { "UserId", "WeaknessScore" });

            migrationBuilder.CreateIndex(
                name: "UX_user_subtopic_stats_user_subtopic",
                table: "user_subtopic_stats",
                columns: new[] { "UserId", "SubtopicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_topic_stats_user_accuracy",
                table: "user_topic_stats",
                columns: new[] { "UserId", "Accuracy" });

            migrationBuilder.CreateIndex(
                name: "IX_user_topic_stats_user_weakness_score",
                table: "user_topic_stats",
                columns: new[] { "UserId", "WeaknessScore" });

            migrationBuilder.CreateIndex(
                name: "UX_user_topic_stats_user_topic",
                table: "user_topic_stats",
                columns: new[] { "UserId", "TopicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_weakness_user_level",
                table: "user_weakness",
                columns: new[] { "UserId", "WeaknessLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_user_weakness_user_subtopic",
                table: "user_weakness",
                columns: new[] { "UserId", "SubtopicId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_weakness_user_topic",
                table: "user_weakness",
                columns: new[] { "UserId", "TopicId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_weakness_user_updated",
                table: "user_weakness",
                columns: new[] { "UserId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_user_weakness_user_topic_subtopic",
                table: "user_weakness",
                columns: new[] { "UserId", "TopicId", "SubtopicId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quiz_attempt");

            migrationBuilder.DropTable(
                name: "user_subtopic_stats");

            migrationBuilder.DropTable(
                name: "user_topic_stats");

            migrationBuilder.DropTable(
                name: "user_weakness");
        }
    }
}

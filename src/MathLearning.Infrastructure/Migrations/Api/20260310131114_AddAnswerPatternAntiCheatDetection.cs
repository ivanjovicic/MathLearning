using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddAnswerPatternAntiCheatDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "answer_pattern_detection_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: true),
                    SubtopicId = table.Column<int>(type: "integer", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClientSequence = table.Column<long>(type: "bigint", nullable: true),
                    AnsweredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "integer", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    AnswerLength = table.Column<int>(type: "integer", nullable: false),
                    AnswerFingerprint = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RiskScore = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SignalsJson = table.Column<string>(type: "jsonb", nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PromptPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    DetectionEngine = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DetectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_answer_pattern_detection_log", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_answer_pattern_detection_review_detected",
                table: "answer_pattern_detection_log",
                columns: new[] { "ReviewStatus", "DetectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_answer_pattern_detection_severity_detected",
                table: "answer_pattern_detection_log",
                columns: new[] { "Severity", "DetectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_answer_pattern_detection_source_answered",
                table: "answer_pattern_detection_log",
                columns: new[] { "SourceType", "AnsweredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_answer_pattern_detection_user_detected",
                table: "answer_pattern_detection_log",
                columns: new[] { "UserId", "DetectedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "answer_pattern_detection_log");
        }
    }
}

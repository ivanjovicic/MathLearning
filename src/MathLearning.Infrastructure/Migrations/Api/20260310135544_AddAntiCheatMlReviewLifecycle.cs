using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddAntiCheatMlReviewLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MlLastAttemptAtUtc",
                table: "answer_pattern_detection_log",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MlLastError",
                table: "answer_pattern_detection_log",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MlModelName",
                table: "answer_pattern_detection_log",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MlReviewAttempts",
                table: "answer_pattern_detection_log",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MlReviewOutputJson",
                table: "answer_pattern_detection_log",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MlReviewStatus",
                table: "answer_pattern_detection_log",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "MlReviewedAtUtc",
                table: "answer_pattern_detection_log",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_answer_pattern_detection_ml_review_detected",
                table: "answer_pattern_detection_log",
                columns: new[] { "MlReviewStatus", "DetectedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_answer_pattern_detection_ml_review_detected",
                table: "answer_pattern_detection_log");

            migrationBuilder.DropColumn(
                name: "MlLastAttemptAtUtc",
                table: "answer_pattern_detection_log");

            migrationBuilder.DropColumn(
                name: "MlLastError",
                table: "answer_pattern_detection_log");

            migrationBuilder.DropColumn(
                name: "MlModelName",
                table: "answer_pattern_detection_log");

            migrationBuilder.DropColumn(
                name: "MlReviewAttempts",
                table: "answer_pattern_detection_log");

            migrationBuilder.DropColumn(
                name: "MlReviewOutputJson",
                table: "answer_pattern_detection_log");

            migrationBuilder.DropColumn(
                name: "MlReviewStatus",
                table: "answer_pattern_detection_log");

            migrationBuilder.DropColumn(
                name: "MlReviewedAtUtc",
                table: "answer_pattern_detection_log");
        }
    }
}

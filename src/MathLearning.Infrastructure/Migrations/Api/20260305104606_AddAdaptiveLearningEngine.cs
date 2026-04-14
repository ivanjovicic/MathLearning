using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddAdaptiveLearningEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "adaptive_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    ProfileDifficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Medium")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adaptive_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "review_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    EasinessFactor = table.Column<double>(type: "double precision", nullable: false, defaultValue: 2.5),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    RepetitionCount = table.Column<int>(type: "integer", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastWasCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_review_schedules_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_review_schedules_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_learning_profiles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    PreferredDifficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Medium"),
                    RollingAccuracy = table.Column<double>(type: "double precision", nullable: false),
                    RollingAverageResponseSeconds = table.Column<double>(type: "double precision", nullable: false),
                    RollingWindowSize = table.Column<int>(type: "integer", nullable: false, defaultValue: 20),
                    TotalAttempts = table.Column<int>(type: "integer", nullable: false),
                    TotalCorrect = table.Column<int>(type: "integer", nullable: false),
                    LastPracticeAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastDifficultyChangeAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_learning_profiles", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "user_question_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    SubtopicId = table.Column<int>(type: "integer", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    ResponseTimeSeconds = table.Column<int>(type: "integer", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Medium"),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AdaptiveSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdaptiveSessionItemId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_question_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_question_history_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_question_history_Subtopics_SubtopicId",
                        column: x => x.SubtopicId,
                        principalTable: "Subtopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_question_history_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_topic_mastery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    MasteryScore = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CorrectAttempts = table.Column<int>(type: "integer", nullable: false),
                    AverageConfidence = table.Column<double>(type: "double precision", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Medium"),
                    IsWeak = table.Column<bool>(type: "boolean", nullable: false),
                    WeakDetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastPracticedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_topic_mastery", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_topic_mastery_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adaptive_session_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdaptiveSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    SubtopicId = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DifficultyLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Medium"),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    ResponseTimeSeconds = table.Column<int>(type: "integer", nullable: true),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adaptive_session_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_adaptive_session_items_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_adaptive_session_items_Subtopics_SubtopicId",
                        column: x => x.SubtopicId,
                        principalTable: "Subtopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_adaptive_session_items_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_adaptive_session_items_adaptive_sessions_AdaptiveSessionId",
                        column: x => x.AdaptiveSessionId,
                        principalTable: "adaptive_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_adaptive_session_items_QuestionId",
                table: "adaptive_session_items",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_adaptive_session_items_SubtopicId",
                table: "adaptive_session_items",
                column: "SubtopicId");

            migrationBuilder.CreateIndex(
                name: "IX_AdaptiveSessionItems_Topic_Difficulty",
                table: "adaptive_session_items",
                columns: new[] { "TopicId", "DifficultyLevel" });

            migrationBuilder.CreateIndex(
                name: "UX_AdaptiveSessionItems_Session_Sequence",
                table: "adaptive_session_items",
                columns: new[] { "AdaptiveSessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdaptiveSessions_User_CreatedAt",
                table: "adaptive_sessions",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_review_schedules_QuestionId",
                table: "review_schedules",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_review_schedules_TopicId",
                table: "review_schedules",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSchedules_User_DueAt",
                table: "review_schedules",
                columns: new[] { "UserId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSchedules_User_Topic",
                table: "review_schedules",
                columns: new[] { "UserId", "TopicId" });

            migrationBuilder.CreateIndex(
                name: "UX_ReviewSchedules_User_Question",
                table: "review_schedules",
                columns: new[] { "UserId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLearningProfiles_User_Difficulty",
                table: "user_learning_profiles",
                columns: new[] { "UserId", "PreferredDifficulty" });

            migrationBuilder.CreateIndex(
                name: "IX_user_question_history_QuestionId",
                table: "user_question_history",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_question_history_SubtopicId",
                table: "user_question_history",
                column: "SubtopicId");

            migrationBuilder.CreateIndex(
                name: "IX_user_question_history_TopicId",
                table: "user_question_history",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_UserQuestionHistory_User_AnsweredAt",
                table: "user_question_history",
                columns: new[] { "UserId", "AnsweredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserQuestionHistory_User_Question",
                table: "user_question_history",
                columns: new[] { "UserId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserQuestionHistory_User_Topic",
                table: "user_question_history",
                columns: new[] { "UserId", "TopicId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_topic_mastery_TopicId",
                table: "user_topic_mastery",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTopicMastery_User_Difficulty",
                table: "user_topic_mastery",
                columns: new[] { "UserId", "DifficultyLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTopicMastery_User_IsWeak",
                table: "user_topic_mastery",
                columns: new[] { "UserId", "IsWeak" });

            migrationBuilder.CreateIndex(
                name: "UX_UserTopicMastery_User_Topic",
                table: "user_topic_mastery",
                columns: new[] { "UserId", "TopicId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adaptive_session_items");

            migrationBuilder.DropTable(
                name: "review_schedules");

            migrationBuilder.DropTable(
                name: "user_learning_profiles");

            migrationBuilder.DropTable(
                name: "user_question_history");

            migrationBuilder.DropTable(
                name: "user_topic_mastery");

            migrationBuilder.DropTable(
                name: "adaptive_sessions");
        }
    }
}

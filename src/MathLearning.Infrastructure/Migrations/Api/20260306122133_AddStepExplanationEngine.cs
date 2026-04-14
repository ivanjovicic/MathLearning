using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddStepExplanationEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "common_mistake_patterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Topic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subtopic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MistakeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PatternKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Remediation = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_common_mistake_patterns", x => x.Id);
                });

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
                name: "math_formula_reference",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Latex = table.Column<string>(type: "TEXT", nullable: false),
                    MathMl = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_math_formula_reference", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "math_transformation_rules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ExpressionPattern = table.Column<string>(type: "TEXT", nullable: true),
                    StepType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExampleLatex = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_math_transformation_rules", x => x.Id);
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
                name: "step_explanation_cache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Grade = table.Column<int>(type: "integer", nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_step_explanation_cache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "step_explanation_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StepType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TemplateText = table.Column<string>(type: "TEXT", nullable: false),
                    HintTemplate = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_step_explanation_template", x => x.Id);
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
                name: "IX_common_mistake_patterns_topic_subtopic_type",
                table: "common_mistake_patterns",
                columns: new[] { "Topic", "Subtopic", "MistakeType" });

            migrationBuilder.CreateIndex(
                name: "IX_common_mistake_patterns_type_priority",
                table: "common_mistake_patterns",
                columns: new[] { "MistakeType", "Priority" });

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
                name: "IX_math_formula_reference_name",
                table: "math_formula_reference",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_math_transformation_rules_active_step",
                table: "math_transformation_rules",
                columns: new[] { "IsActive", "StepType" });

            migrationBuilder.CreateIndex(
                name: "IX_practice_session_user_started_at",
                table: "practice_session",
                columns: new[] { "UserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_practice_session_user_status",
                table: "practice_session",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_practice_session_user_subtopic",
                table: "practice_session",
                columns: new[] { "UserId", "SubtopicId" });

            migrationBuilder.CreateIndex(
                name: "IX_practice_session_user_topic",
                table: "practice_session",
                columns: new[] { "UserId", "TopicId" });

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

            migrationBuilder.CreateIndex(
                name: "IX_step_explanation_cache_expires_at",
                table: "step_explanation_cache",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "UX_step_explanation_cache_problem_grade_difficulty",
                table: "step_explanation_cache",
                columns: new[] { "ProblemHash", "Grade", "Difficulty" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_step_explanation_template_rule_lang_step",
                table: "step_explanation_template",
                columns: new[] { "RuleKey", "Language", "StepType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "common_mistake_patterns");

            migrationBuilder.DropTable(
                name: "mastery_state");

            migrationBuilder.DropTable(
                name: "math_formula_reference");

            migrationBuilder.DropTable(
                name: "math_transformation_rules");

            migrationBuilder.DropTable(
                name: "practice_session_item");

            migrationBuilder.DropTable(
                name: "step_explanation_cache");

            migrationBuilder.DropTable(
                name: "step_explanation_template");

            migrationBuilder.DropTable(
                name: "practice_session");
        }
    }
}

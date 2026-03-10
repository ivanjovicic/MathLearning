using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddQuestionAuthoringPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentDraftId",
                table: "Questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentVersionNumber",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "HintFull",
                table: "Questions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishState",
                table: "Questions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "draft");

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAtUtc",
                table: "Questions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedByUserId",
                table: "Questions",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "question_authoring_audit_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuestionId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: true),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_authoring_audit_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_authoring_audit_log_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "question_drafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: true),
                    PreviousDraftId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftVersion = table.Column<int>(type: "integer", nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    NormalizedContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PublishState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ValidationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AuthorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    EditorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    LatestValidationResultId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_drafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_drafts_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_preview_cache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PreviewPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_preview_cache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_preview_cache_question_drafts_DraftId",
                        column: x => x.DraftId,
                        principalTable: "question_drafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_validation_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HasErrors = table.Column<bool>(type: "boolean", nullable: false),
                    HasWarnings = table.Column<bool>(type: "boolean", nullable: false),
                    IssueCount = table.Column<int>(type: "integer", nullable: false),
                    SummaryJson = table.Column<string>(type: "jsonb", nullable: true),
                    PreviewPayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    ValidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_validation_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_validation_results_question_drafts_DraftId",
                        column: x => x.DraftId,
                        principalTable: "question_drafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_versions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    SourceDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousVersionId = table.Column<long>(type: "bigint", nullable: true),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    NormalizedSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    PublishState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AuthorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    EditorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_versions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_question_versions_question_drafts_SourceDraftId",
                        column: x => x.SourceDraftId,
                        principalTable: "question_drafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_question_versions_question_versions_PreviousVersionId",
                        column: x => x.PreviousVersionId,
                        principalTable: "question_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "question_validation_issues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ValidationResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RuleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FieldPath = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Suggestion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_validation_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_validation_issues_question_validation_results_Vali~",
                        column: x => x.ValidationResultId,
                        principalTable: "question_validation_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_CurrentDraftId",
                table: "Questions",
                column: "CurrentDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_PublishState",
                table: "Questions",
                column: "PublishState");

            migrationBuilder.CreateIndex(
                name: "IX_question_authoring_audit_draft_occurred",
                table: "question_authoring_audit_log",
                columns: new[] { "DraftId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_question_authoring_audit_question_occurred",
                table: "question_authoring_audit_log",
                columns: new[] { "QuestionId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_question_drafts_content_hash",
                table: "question_drafts",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_question_drafts_LatestValidationResultId",
                table: "question_drafts",
                column: "LatestValidationResultId");

            migrationBuilder.CreateIndex(
                name: "UX_question_drafts_question_version",
                table: "question_drafts",
                columns: new[] { "QuestionId", "DraftVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_question_preview_cache_content_hash",
                table: "question_preview_cache",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_question_preview_cache_DraftId",
                table: "question_preview_cache",
                column: "DraftId");

            migrationBuilder.CreateIndex(
                name: "IX_question_preview_cache_expires",
                table: "question_preview_cache",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_question_validation_issues_result_stage",
                table: "question_validation_issues",
                columns: new[] { "ValidationResultId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_question_validation_results_draft_validated",
                table: "question_validation_results",
                columns: new[] { "DraftId", "ValidatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_question_versions_PreviousVersionId",
                table: "question_versions",
                column: "PreviousVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_question_versions_question_published_at",
                table: "question_versions",
                columns: new[] { "QuestionId", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_question_versions_SourceDraftId",
                table: "question_versions",
                column: "SourceDraftId");

            migrationBuilder.CreateIndex(
                name: "UX_question_versions_question_version",
                table: "question_versions",
                columns: new[] { "QuestionId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_question_authoring_audit_log_question_drafts_DraftId",
                table: "question_authoring_audit_log",
                column: "DraftId",
                principalTable: "question_drafts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_question_drafts_question_validation_results_LatestValidatio~",
                table: "question_drafts",
                column: "LatestValidationResultId",
                principalTable: "question_validation_results",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_question_validation_results_question_drafts_DraftId",
                table: "question_validation_results");

            migrationBuilder.DropTable(
                name: "question_authoring_audit_log");

            migrationBuilder.DropTable(
                name: "question_preview_cache");

            migrationBuilder.DropTable(
                name: "question_validation_issues");

            migrationBuilder.DropTable(
                name: "question_versions");

            migrationBuilder.DropTable(
                name: "question_drafts");

            migrationBuilder.DropTable(
                name: "question_validation_results");

            migrationBuilder.DropIndex(
                name: "IX_Questions_CurrentDraftId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_PublishState",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "CurrentDraftId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "CurrentVersionNumber",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "HintFull",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "PublishState",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "PublishedAtUtc",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "PublishedByUserId",
                table: "Questions");
        }
    }
}

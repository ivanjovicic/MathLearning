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
            migrationBuilder.Sql("""
                ALTER TABLE "Questions"
                ADD COLUMN IF NOT EXISTS "CurrentDraftId" uuid NULL;

                ALTER TABLE "Questions"
                ADD COLUMN IF NOT EXISTS "CurrentVersionNumber" integer NOT NULL DEFAULT 0;

                ALTER TABLE "Questions"
                ADD COLUMN IF NOT EXISTS "HintFull" TEXT NULL;

                ALTER TABLE "Questions"
                ADD COLUMN IF NOT EXISTS "PublishState" character varying(32) NOT NULL DEFAULT 'draft';

                ALTER TABLE "Questions"
                ADD COLUMN IF NOT EXISTS "PublishedAtUtc" timestamp with time zone NULL;

                ALTER TABLE "Questions"
                ADD COLUMN IF NOT EXISTS "PublishedByUserId" character varying(450) NULL;
            """);

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

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Questions_CurrentDraftId" ON "Questions" ("CurrentDraftId");
                CREATE INDEX IF NOT EXISTS "IX_Questions_PublishState" ON "Questions" ("PublishState");
            """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_question_authoring_audit_draft_occurred" ON "question_authoring_audit_log" ("DraftId", "OccurredAtUtc");
                CREATE INDEX IF NOT EXISTS "IX_question_authoring_audit_question_occurred" ON "question_authoring_audit_log" ("QuestionId", "OccurredAtUtc");
                CREATE INDEX IF NOT EXISTS "IX_question_drafts_content_hash" ON "question_drafts" ("ContentHash");
                CREATE INDEX IF NOT EXISTS "IX_question_drafts_LatestValidationResultId" ON "question_drafts" ("LatestValidationResultId");
                CREATE UNIQUE INDEX IF NOT EXISTS "UX_question_drafts_question_version" ON "question_drafts" ("QuestionId", "DraftVersion");
                CREATE INDEX IF NOT EXISTS "IX_question_preview_cache_content_hash" ON "question_preview_cache" ("ContentHash");
                CREATE INDEX IF NOT EXISTS "IX_question_preview_cache_DraftId" ON "question_preview_cache" ("DraftId");
                CREATE INDEX IF NOT EXISTS "IX_question_preview_cache_expires" ON "question_preview_cache" ("ExpiresAtUtc");
                CREATE INDEX IF NOT EXISTS "IX_question_validation_issues_result_stage" ON "question_validation_issues" ("ValidationResultId", "Stage");
                CREATE INDEX IF NOT EXISTS "IX_question_validation_results_draft_validated" ON "question_validation_results" ("DraftId", "ValidatedAtUtc");
                CREATE INDEX IF NOT EXISTS "IX_question_versions_PreviousVersionId" ON "question_versions" ("PreviousVersionId");
                CREATE INDEX IF NOT EXISTS "IX_question_versions_question_published_at" ON "question_versions" ("QuestionId", "PublishedAtUtc");
                CREATE INDEX IF NOT EXISTS "IX_question_versions_SourceDraftId" ON "question_versions" ("SourceDraftId");
                CREATE UNIQUE INDEX IF NOT EXISTS "UX_question_versions_question_version" ON "question_versions" ("QuestionId", "VersionNumber");
            """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'FK_question_authoring_audit_log_question_drafts_DraftId'
                    ) THEN
                        ALTER TABLE "question_authoring_audit_log"
                        ADD CONSTRAINT "FK_question_authoring_audit_log_question_drafts_DraftId"
                        FOREIGN KEY ("DraftId") REFERENCES "question_drafts" ("Id") ON DELETE SET NULL;
                    END IF;
                END $$;

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'FK_question_drafts_question_validation_results_LatestValidationResultId'
                    ) THEN
                        ALTER TABLE "question_drafts"
                        ADD CONSTRAINT "FK_question_drafts_question_validation_results_LatestValidationResultId"
                        FOREIGN KEY ("LatestValidationResultId") REFERENCES "question_validation_results" ("Id") ON DELETE SET NULL;
                    END IF;
                END $$;
            """);
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

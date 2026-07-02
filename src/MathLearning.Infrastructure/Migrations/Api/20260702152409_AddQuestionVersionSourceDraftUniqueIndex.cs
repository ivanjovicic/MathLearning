using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddQuestionVersionSourceDraftUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_question_versions_SourceDraftId",
                table: "question_versions");

            migrationBuilder.CreateIndex(
                name: "UX_question_versions_source_draft",
                table: "question_versions",
                column: "SourceDraftId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_question_versions_source_draft",
                table: "question_versions");

            migrationBuilder.CreateIndex(
                name: "IX_question_versions_SourceDraftId",
                table: "question_versions",
                column: "SourceDraftId");
        }
    }
}

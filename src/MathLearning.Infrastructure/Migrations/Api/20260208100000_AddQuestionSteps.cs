using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddQuestionSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    StepIndex = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Hint = table.Column<string>(type: "TEXT", nullable: true),
                    Highlight = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionSteps_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionStepTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionStepId = table.Column<int>(type: "integer", nullable: false),
                    Lang = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Hint = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionStepTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionStepTranslations_QuestionSteps_QuestionStepId",
                        column: x => x.QuestionStepId,
                        principalTable: "QuestionSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionSteps_Question_Index",
                table: "QuestionSteps",
                columns: new[] { "QuestionId", "StepIndex" });

            migrationBuilder.CreateIndex(
                name: "UX_QuestionStepTranslations_Step_Lang",
                table: "QuestionStepTranslations",
                columns: new[] { "QuestionStepId", "Lang" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionStepTranslations");

            migrationBuilder.DropTable(
                name: "QuestionSteps");
        }
    }
}

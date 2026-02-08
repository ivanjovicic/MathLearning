using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OptionTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptionId = table.Column<int>(type: "integer", nullable: false),
                    Lang = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OptionTranslations_Options_OptionId",
                        column: x => x.OptionId,
                        principalTable: "Options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    Lang = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", nullable: true),
                    HintFormula = table.Column<string>(type: "TEXT", nullable: true),
                    HintClue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionTranslations_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_OptionTranslations_Option_Lang",
                table: "OptionTranslations",
                columns: new[] { "OptionId", "Lang" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionTranslations_Lang",
                table: "QuestionTranslations",
                column: "Lang");

            migrationBuilder.CreateIndex(
                name: "UX_QuestionTranslations_Question_Lang",
                table: "QuestionTranslations",
                columns: new[] { "QuestionId", "Lang" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OptionTranslations");

            migrationBuilder.DropTable(
                name: "QuestionTranslations");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddHintLevelsToQuestionTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HintFull",
                table: "QuestionTranslations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HintLight",
                table: "QuestionTranslations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HintMedium",
                table: "QuestionTranslations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HintFull",
                table: "QuestionTranslations");

            migrationBuilder.DropColumn(
                name: "HintLight",
                table: "QuestionTranslations");

            migrationBuilder.DropColumn(
                name: "HintMedium",
                table: "QuestionTranslations");
        }
    }
}

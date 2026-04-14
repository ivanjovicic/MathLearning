using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddMathContentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HintFormat",
                table: "QuestionSteps",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "MarkdownWithMath");

            migrationBuilder.AddColumn<string>(
                name: "HintRenderMode",
                table: "QuestionSteps",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<string>(
                name: "SemanticsAltText",
                table: "QuestionSteps",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextFormat",
                table: "QuestionSteps",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "MarkdownWithMath");

            migrationBuilder.AddColumn<string>(
                name: "TextRenderMode",
                table: "QuestionSteps",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<string>(
                name: "ExplanationFormat",
                table: "Questions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "MarkdownWithMath");

            migrationBuilder.AddColumn<string>(
                name: "ExplanationRenderMode",
                table: "Questions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<string>(
                name: "HintFormat",
                table: "Questions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "MarkdownWithMath");

            migrationBuilder.AddColumn<string>(
                name: "HintRenderMode",
                table: "Questions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<string>(
                name: "SemanticsAltText",
                table: "Questions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextFormat",
                table: "Questions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "MarkdownWithMath");

            migrationBuilder.AddColumn<string>(
                name: "TextRenderMode",
                table: "Questions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<string>(
                name: "RenderMode",
                table: "Options",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<string>(
                name: "SemanticsAltText",
                table: "Options",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextFormat",
                table: "Options",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "MarkdownWithMath");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HintFormat",
                table: "QuestionSteps");

            migrationBuilder.DropColumn(
                name: "HintRenderMode",
                table: "QuestionSteps");

            migrationBuilder.DropColumn(
                name: "SemanticsAltText",
                table: "QuestionSteps");

            migrationBuilder.DropColumn(
                name: "TextFormat",
                table: "QuestionSteps");

            migrationBuilder.DropColumn(
                name: "TextRenderMode",
                table: "QuestionSteps");

            migrationBuilder.DropColumn(
                name: "ExplanationFormat",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "ExplanationRenderMode",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "HintFormat",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "HintRenderMode",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "SemanticsAltText",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TextFormat",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TextRenderMode",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "RenderMode",
                table: "Options");

            migrationBuilder.DropColumn(
                name: "SemanticsAltText",
                table: "Options");

            migrationBuilder.DropColumn(
                name: "TextFormat",
                table: "Options");
        }
    }
}

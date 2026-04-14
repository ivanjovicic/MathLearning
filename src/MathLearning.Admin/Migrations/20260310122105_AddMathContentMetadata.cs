using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddMathContentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Hint",
                table: "QuestionStep",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HintFormat",
                table: "QuestionStep",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "MarkdownWithMath");

            migrationBuilder.AddColumn<string>(
                name: "HintRenderMode",
                table: "QuestionStep",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<string>(
                name: "SemanticsAltText",
                table: "QuestionStep",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextFormat",
                table: "QuestionStep",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "MarkdownWithMath");

            migrationBuilder.AddColumn<string>(
                name: "TextRenderMode",
                table: "QuestionStep",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Auto");

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
                defaultValue: "draft");

            migrationBuilder.AddColumn<string>(
                name: "HintFull",
                table: "Questions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HintRenderMode",
                table: "Questions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "MarkdownWithMath");

            migrationBuilder.AddColumn<string>(
                name: "PublishState",
                table: "Questions",
                type: "text",
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAtUtc",
                table: "Questions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedByUserId",
                table: "Questions",
                type: "text",
                nullable: true);

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
                defaultValue: "Auto");

            migrationBuilder.AddColumn<string>(
                name: "TextRenderMode",
                table: "Questions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "MarkdownWithMath");

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
                table: "QuestionStep");

            migrationBuilder.DropColumn(
                name: "HintRenderMode",
                table: "QuestionStep");

            migrationBuilder.DropColumn(
                name: "SemanticsAltText",
                table: "QuestionStep");

            migrationBuilder.DropColumn(
                name: "TextFormat",
                table: "QuestionStep");

            migrationBuilder.DropColumn(
                name: "TextRenderMode",
                table: "QuestionStep");

            migrationBuilder.DropColumn(
                name: "CurrentDraftId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "CurrentVersionNumber",
                table: "Questions");

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
                name: "HintFull",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "HintRenderMode",
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

            migrationBuilder.AlterColumn<string>(
                name: "Hint",
                table: "QuestionStep",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}

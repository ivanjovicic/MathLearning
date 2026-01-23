using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddHintSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HintClue",
                table: "Questions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HintDifficulty",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "HintFormula",
                table: "Questions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserHints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    HintType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserHints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserHints_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserHints_QuestionId",
                table: "UserHints",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserHints_UsedAt",
                table: "UserHints",
                column: "UsedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserHints_User_Question",
                table: "UserHints",
                columns: new[] { "UserId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserHints_UserId",
                table: "UserHints",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserHints");

            migrationBuilder.DropColumn(
                name: "HintClue",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "HintDifficulty",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "HintFormula",
                table: "Questions");
        }
    }
}

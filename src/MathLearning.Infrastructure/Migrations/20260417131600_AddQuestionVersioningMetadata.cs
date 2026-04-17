using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260417131600_AddQuestionVersioningMetadata")]
    public partial class AddQuestionVersioningMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousSnapshotJson",
                table: "Questions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Questions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousSnapshotJson",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Questions");
        }
    }
}

using MathLearning.Admin.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Admin.Migrations
{
    [DbContext(typeof(AdminDbContext))]
    [Migration("20260417131500_AddQuestionVersioningMetadata")]
    public partial class AddQuestionVersioningMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
ALTER TABLE "Questions"
ADD COLUMN IF NOT EXISTS "PreviousSnapshotJson" jsonb;
""");

            migrationBuilder.Sql("""
ALTER TABLE "Questions"
ADD COLUMN IF NOT EXISTS "UpdatedBy" character varying(256);
""");
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

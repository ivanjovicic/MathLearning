using MathLearning.Admin.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Admin.Migrations
{
    [DbContext(typeof(AdminDbContext))]
    [Migration("20260501000001_AddSoftDeleteToQuestions")]
    public partial class AddSoftDeleteToQuestions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
ALTER TABLE "Questions"
    ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone NULL;
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_Questions_IsDeleted" ON "Questions" ("IsDeleted");
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Questions_IsDeleted";""");
            migrationBuilder.Sql("""
ALTER TABLE "Questions"
    DROP COLUMN IF EXISTS "IsDeleted",
    DROP COLUMN IF EXISTS "DeletedAt";
""");
        }
    }
}

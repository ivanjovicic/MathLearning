using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MathLearning.Infrastructure.Persistance;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api;

[DbContext(typeof(ApiDbContext))]
[Migration("20260307134000_EnsureLastXpResetDateColumn")]
public partial class EnsureLastXpResetDateColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UserProfiles"
            ADD COLUMN IF NOT EXISTS "LastXpResetDate" timestamp with time zone NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UserProfiles"
            DROP COLUMN IF EXISTS "LastXpResetDate";
            """);
    }
}

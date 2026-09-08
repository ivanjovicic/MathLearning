using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api;

[DbContext(typeof(ApiDbContext))]
[Migration("20260908130000_EnsureSyncDeadLetterRedriveSchema")]
public partial class EnsureSyncDeadLetterRedriveSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE IF EXISTS "SyncDeadLetter"
            ADD COLUMN IF NOT EXISTS "LastRedriveAttemptAtUtc" timestamp with time zone NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This is a forward-only repair migration. The column may have existed before it ran.
    }
}

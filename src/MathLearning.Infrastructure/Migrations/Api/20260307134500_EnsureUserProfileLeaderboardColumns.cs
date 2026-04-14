using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MathLearning.Infrastructure.Persistance;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api;

[DbContext(typeof(ApiDbContext))]
[Migration("20260307134500_EnsureUserProfileLeaderboardColumns")]
public partial class EnsureUserProfileLeaderboardColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "DailyXp" integer NOT NULL DEFAULT 0;
            ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "WeeklyXp" integer NOT NULL DEFAULT 0;
            ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "MonthlyXp" integer NOT NULL DEFAULT 0;
            ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "LastXpResetDate" timestamp with time zone NULL;
            ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "LeaderboardOptIn" boolean NOT NULL DEFAULT TRUE;
            ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "SchoolId" integer NULL;
            ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "SchoolName" text NULL;
            ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "FacultyId" integer NULL;
            ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "FacultyName" text NULL;
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_UserProfiles_Leaderboard_DailyXp"
            ON "UserProfiles" ("LeaderboardOptIn", "DailyXp");
            CREATE INDEX IF NOT EXISTS "IX_UserProfiles_Leaderboard_WeeklyXp"
            ON "UserProfiles" ("LeaderboardOptIn", "WeeklyXp");
            CREATE INDEX IF NOT EXISTS "IX_UserProfiles_Leaderboard_MonthlyXp"
            ON "UserProfiles" ("LeaderboardOptIn", "MonthlyXp");
            CREATE INDEX IF NOT EXISTS "IX_UserProfiles_School_Leaderboard"
            ON "UserProfiles" ("SchoolId", "LeaderboardOptIn");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_UserProfiles_School_Leaderboard";
            DROP INDEX IF EXISTS "IX_UserProfiles_Leaderboard_MonthlyXp";
            DROP INDEX IF EXISTS "IX_UserProfiles_Leaderboard_WeeklyXp";
            DROP INDEX IF EXISTS "IX_UserProfiles_Leaderboard_DailyXp";
            """);
    }
}

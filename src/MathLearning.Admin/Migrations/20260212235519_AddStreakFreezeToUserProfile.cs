using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddStreakFreezeToUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF to_regclass('public."UserProfiles"') IS NULL THEN
        RETURN;
    END IF;

    ALTER TABLE "UserProfiles"
        ADD COLUMN IF NOT EXISTS "StreakFreezeCount" integer NOT NULL DEFAULT 0,
        ADD COLUMN IF NOT EXISTS "LastActivityDay" date NULL,
        ADD COLUMN IF NOT EXISTS "LastStreakDay" date NULL;
END
$$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non-destructive: UserProfiles is owned by the API migration stream.
        }
    }
}

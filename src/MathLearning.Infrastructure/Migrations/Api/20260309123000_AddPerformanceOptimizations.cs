using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api;

public partial class AddPerformanceOptimizations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS leaderboard_snapshot (
                "Id" bigserial PRIMARY KEY,
                "UserId" character varying(450) NOT NULL,
                "Scope" character varying(64) NOT NULL,
                "Period" character varying(32) NOT NULL,
                "Rank" integer NOT NULL,
                "Score" integer NOT NULL,
                "DisplayName" character varying(256) NOT NULL,
                "Level" integer NOT NULL,
                "Streak" integer NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL
            );
            """);

        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS user_quiz_summary (
                "UserId" character varying(450) PRIMARY KEY,
                "TotalCorrect" integer NOT NULL DEFAULT 0,
                "TotalAttempts" integer NOT NULL DEFAULT 0,
                "WeeklyCorrect" integer NOT NULL DEFAULT 0,
                "WeeklyXp" integer NOT NULL DEFAULT 0,
                "UpdatedAtUtc" timestamp with time zone NOT NULL
            );
            """);

        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS user_reward_state (
                "Id" bigserial PRIMARY KEY,
                "UserId" character varying(450) NOT NULL,
                "RewardKey" character varying(128) NOT NULL,
                "Eligible" boolean NOT NULL DEFAULT FALSE,
                "Claimed" boolean NOT NULL DEFAULT FALSE,
                "ClaimedAtUtc" timestamp with time zone NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL
            );
            """);

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "UX_leaderboard_snapshot_scope_period_user"
                ON leaderboard_snapshot ("Scope", "Period", "UserId");
            """,
            suppressTransaction: true);

        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_leaderboard_snapshot_scope_period_rank"
                ON leaderboard_snapshot ("Scope", "Period", "Rank");
            """,
            suppressTransaction: true);

        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_user_quiz_summary_updated_at"
                ON user_quiz_summary ("UpdatedAtUtc");
            """,
            suppressTransaction: true);

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "UX_user_reward_state_user_reward"
                ON user_reward_state ("UserId", "RewardKey");
            """,
            suppressTransaction: true);

        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_user_reward_state_user_status"
                ON user_reward_state ("UserId", "Eligible", "Claimed");
            """,
            suppressTransaction: true);

        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_user_profiles_leaderboard_totalxp_covering"
                ON "UserProfiles" ("LeaderboardOptIn", "Xp" DESC)
                INCLUDE ("DisplayName", "Level", "Streak");
            """,
            suppressTransaction: true);

        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_user_profiles_school_aggregation_covering"
                ON "UserProfiles" ("SchoolId", "DailyXp", "WeeklyXp", "MonthlyXp")
                WHERE "SchoolId" IS NOT NULL;
            """,
            suppressTransaction: true);

        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_cosmetic_items_active_catalog"
                ON cosmetic_items ("Category", "Rarity", "SortOrder")
                WHERE "IsActive" = TRUE AND "IsHidden" = FALSE;
            """,
            suppressTransaction: true);

        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_user_cosmetic_inventory_user_active_item"
                ON user_cosmetic_inventory ("UserId", "IsRevoked", "CosmeticItemId");
            """,
            suppressTransaction: true);

        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_school_scores_period_start_rank_covering"
                ON school_scores ("Period", "PeriodStartUtc", "Rank")
                INCLUDE ("SchoolId", "XpTotal", "CompositeScore", "UpdatedAtUtc");
            """,
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "IX_school_scores_period_start_rank_covering";""", suppressTransaction: true);
        migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "IX_user_cosmetic_inventory_user_active_item";""", suppressTransaction: true);
        migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "IX_cosmetic_items_active_catalog";""", suppressTransaction: true);
        migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "IX_user_profiles_school_aggregation_covering";""", suppressTransaction: true);
        migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "IX_user_profiles_leaderboard_totalxp_covering";""", suppressTransaction: true);
        migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "IX_user_reward_state_user_status";""", suppressTransaction: true);
        migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "UX_user_reward_state_user_reward";""", suppressTransaction: true);
        migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "IX_user_quiz_summary_updated_at";""", suppressTransaction: true);
        migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "IX_leaderboard_snapshot_scope_period_rank";""", suppressTransaction: true);
        migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "UX_leaderboard_snapshot_scope_period_user";""", suppressTransaction: true);

        migrationBuilder.Sql("""DROP TABLE IF EXISTS user_reward_state;""");
        migrationBuilder.Sql("""DROP TABLE IF EXISTS user_quiz_summary;""");
        migrationBuilder.Sql("""DROP TABLE IF EXISTS leaderboard_snapshot;""");
    }
}

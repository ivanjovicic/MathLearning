using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddSeasonAndAntiCheat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SeasonId",
                table: "user_xp_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SeasonId",
                table: "school_scores",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WeightedXp",
                table: "school_scores",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<Guid>(
                name: "SeasonId",
                table: "school_rank_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WeightedXp",
                table: "school_rank_history",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "competition_seasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_competition_seasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "leaderboard_snapshot",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Streak = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leaderboard_snapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_quiz_summary",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TotalCorrect = table.Column<int>(type: "integer", nullable: false),
                    TotalAttempts = table.Column<int>(type: "integer", nullable: false),
                    WeeklyCorrect = table.Column<int>(type: "integer", nullable: false),
                    WeeklyXp = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_quiz_summary", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "user_reward_state",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RewardKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Eligible = table.Column<bool>(type: "boolean", nullable: false),
                    Claimed = table.Column<bool>(type: "boolean", nullable: false),
                    ClaimedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_reward_state", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "xp_cheat_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    XpDelta = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    DetectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xp_cheat_log", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_xp_events_SeasonId",
                table: "user_xp_events",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_school_scores_SeasonId",
                table: "school_scores",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_school_rank_history_SeasonId",
                table: "school_rank_history",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_competition_seasons_active",
                table: "competition_seasons",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_leaderboard_snapshot_scope_period_rank",
                table: "leaderboard_snapshot",
                columns: new[] { "Scope", "Period", "Rank" });

            migrationBuilder.CreateIndex(
                name: "UX_leaderboard_snapshot_scope_period_user",
                table: "leaderboard_snapshot",
                columns: new[] { "Scope", "Period", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_quiz_summary_updated_at",
                table: "user_quiz_summary",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_user_reward_state_user_status",
                table: "user_reward_state",
                columns: new[] { "UserId", "Eligible", "Claimed" });

            migrationBuilder.CreateIndex(
                name: "UX_user_reward_state_user_reward",
                table: "user_reward_state",
                columns: new[] { "UserId", "RewardKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_xp_cheat_log_detected",
                table: "xp_cheat_log",
                column: "DetectedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_xp_cheat_log_user_detected",
                table: "xp_cheat_log",
                columns: new[] { "UserId", "DetectedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_school_rank_history_competition_seasons_SeasonId",
                table: "school_rank_history",
                column: "SeasonId",
                principalTable: "competition_seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_school_scores_competition_seasons_SeasonId",
                table: "school_scores",
                column: "SeasonId",
                principalTable: "competition_seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_user_xp_events_competition_seasons_SeasonId",
                table: "user_xp_events",
                column: "SeasonId",
                principalTable: "competition_seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_school_rank_history_competition_seasons_SeasonId",
                table: "school_rank_history");

            migrationBuilder.DropForeignKey(
                name: "FK_school_scores_competition_seasons_SeasonId",
                table: "school_scores");

            migrationBuilder.DropForeignKey(
                name: "FK_user_xp_events_competition_seasons_SeasonId",
                table: "user_xp_events");

            migrationBuilder.DropTable(
                name: "competition_seasons");

            migrationBuilder.DropTable(
                name: "leaderboard_snapshot");

            migrationBuilder.DropTable(
                name: "user_quiz_summary");

            migrationBuilder.DropTable(
                name: "user_reward_state");

            migrationBuilder.DropTable(
                name: "xp_cheat_log");

            migrationBuilder.DropIndex(
                name: "IX_user_xp_events_SeasonId",
                table: "user_xp_events");

            migrationBuilder.DropIndex(
                name: "IX_school_scores_SeasonId",
                table: "school_scores");

            migrationBuilder.DropIndex(
                name: "IX_school_rank_history_SeasonId",
                table: "school_rank_history");

            migrationBuilder.DropColumn(
                name: "SeasonId",
                table: "user_xp_events");

            migrationBuilder.DropColumn(
                name: "SeasonId",
                table: "school_scores");

            migrationBuilder.DropColumn(
                name: "WeightedXp",
                table: "school_scores");

            migrationBuilder.DropColumn(
                name: "SeasonId",
                table: "school_rank_history");

            migrationBuilder.DropColumn(
                name: "WeightedXp",
                table: "school_rank_history");
        }
    }
}

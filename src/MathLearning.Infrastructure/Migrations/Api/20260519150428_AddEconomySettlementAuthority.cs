using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddEconomySettlementAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "economy_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_economy_transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_cosmetic_fragment_progress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    FragmentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Copies = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_cosmetic_fragment_progress", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_season_daily_run_claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    DailyRunTransactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DailyRunClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    AwardedXp = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_season_daily_run_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_season_daily_run_claims_cosmetic_seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "cosmetic_seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_season_milestone_claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    MilestoneId = table.Column<int>(type: "integer", nullable: false),
                    RewardType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CoinsAwarded = table.Column<int>(type: "integer", nullable: true),
                    XpAwarded = table.Column<int>(type: "integer", nullable: true),
                    CosmeticItemId = table.Column<int>(type: "integer", nullable: true),
                    FragmentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FragmentCopiesAwarded = table.Column<int>(type: "integer", nullable: true),
                    AlreadyOwned = table.Column<bool>(type: "boolean", nullable: false),
                    ClaimedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_season_milestone_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_season_milestone_claims_cosmetic_seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "cosmetic_seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_season_progress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    EarnedXp = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_season_progress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_season_progress_cosmetic_seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "cosmetic_seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_economy_transactions_user_created_at",
                table: "economy_transactions",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_economy_transactions_user_type_key",
                table: "economy_transactions",
                columns: new[] { "UserId", "TransactionType", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_user_cosmetic_fragment_progress_user_fragment",
                table: "user_cosmetic_fragment_progress",
                columns: new[] { "UserId", "FragmentName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_season_daily_run_claims_SeasonId",
                table: "user_season_daily_run_claims",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_user_season_daily_run_claims_user_season",
                table: "user_season_daily_run_claims",
                columns: new[] { "UserId", "SeasonId" });

            migrationBuilder.CreateIndex(
                name: "UX_user_season_daily_run_claims_user_transaction",
                table: "user_season_daily_run_claims",
                columns: new[] { "UserId", "DailyRunTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_season_milestone_claims_SeasonId",
                table: "user_season_milestone_claims",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_user_season_milestone_claims_user_season_claimed_at",
                table: "user_season_milestone_claims",
                columns: new[] { "UserId", "SeasonId", "ClaimedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_user_season_milestone_claims_user_season_milestone",
                table: "user_season_milestone_claims",
                columns: new[] { "UserId", "SeasonId", "MilestoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_season_progress_SeasonId",
                table: "user_season_progress",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "UX_user_season_progress_user_season",
                table: "user_season_progress",
                columns: new[] { "UserId", "SeasonId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "economy_transactions");

            migrationBuilder.DropTable(
                name: "user_cosmetic_fragment_progress");

            migrationBuilder.DropTable(
                name: "user_season_daily_run_claims");

            migrationBuilder.DropTable(
                name: "user_season_milestone_claims");

            migrationBuilder.DropTable(
                name: "user_season_progress");
        }
    }
}

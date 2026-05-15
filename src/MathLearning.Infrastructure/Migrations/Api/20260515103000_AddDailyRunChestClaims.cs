using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddDailyRunChestClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_run_chest_claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    TransactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RewardSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResponseSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    ClaimedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResultCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_run_chest_claims", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_daily_run_chest_claims_user_claimed_at",
                table: "daily_run_chest_claims",
                columns: new[] { "UserId", "ClaimedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_daily_run_chest_claims_user_day",
                table: "daily_run_chest_claims",
                columns: new[] { "UserId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_daily_run_chest_claims_user_tx",
                table: "daily_run_chest_claims",
                columns: new[] { "UserId", "TransactionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_run_chest_claims");
        }
    }
}


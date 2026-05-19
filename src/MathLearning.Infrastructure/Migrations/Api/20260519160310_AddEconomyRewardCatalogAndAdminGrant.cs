using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddEconomyRewardCatalogAndAdminGrant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "economy_reward_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RewardIdPattern = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RewardType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MatchStrategy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Coins = table.Column<int>(type: "integer", nullable: false),
                    Xp = table.Column<int>(type: "integer", nullable: false),
                    CoinsPerUnit = table.Column<int>(type: "integer", nullable: true),
                    MinCoins = table.Column<int>(type: "integer", nullable: true),
                    MaxCoins = table.Column<int>(type: "integer", nullable: true),
                    IsSingleUse = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_economy_reward_definitions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "economy_reward_definitions",
                columns: new[] { "Id", "Coins", "CoinsPerUnit", "IsActive", "IsSingleUse", "MatchStrategy", "MaxCoins", "MetadataJson", "MinCoins", "RewardIdPattern", "RewardType", "UpdatedAtUtc", "Xp" },
                values: new object[,]
                {
                    { new Guid("2e3d6e31-3f8d-4d60-9266-3cbdb3a34729"), 50, null, true, true, "Exact", null, null, null, "generic:onboarding_bonus", "generic", new DateTime(2026, 5, 19, 0, 0, 0, 0, DateTimeKind.Utc), 0 },
                    { new Guid("7b40d3ba-e74d-4e25-bd84-60d2d645a1c1"), 20, null, true, true, "PrefixNonEmptySuffix", null, null, null, "daily:", "daily", new DateTime(2026, 5, 19, 0, 0, 0, 0, DateTimeKind.Utc), 15 },
                    { new Guid("d4e88c31-56c0-494b-9611-271db4f1dcd8"), 25, null, true, true, "Exact", null, null, null, "generic:starter_bonus", "generic", new DateTime(2026, 5, 19, 0, 0, 0, 0, DateTimeKind.Utc), 0 },
                    { new Guid("d9d5e0d8-87fa-4819-be4a-6285c2ef6fc7"), 0, 10, true, true, "NumericSuffixLevelThreshold", null, null, 10, "level:", "level", new DateTime(2026, 5, 19, 0, 0, 0, 0, DateTimeKind.Utc), 0 },
                    { new Guid("e1f90a77-eeb8-4fd7-973e-e05449b7678a"), 15, null, true, true, "Exact", null, null, null, "generic:welcome_back", "generic", new DateTime(2026, 5, 19, 0, 0, 0, 0, DateTimeKind.Utc), 10 },
                    { new Guid("fa5d14d5-7931-4b57-b5d0-442afc4ba26e"), 0, 5, true, true, "NumericSuffixStreakThreshold", 500, null, 10, "streak:", "streak", new DateTime(2026, 5, 19, 0, 0, 0, 0, DateTimeKind.Utc), 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_economy_reward_definitions_type_active",
                table: "economy_reward_definitions",
                columns: new[] { "RewardType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UX_economy_reward_definitions_type_pattern",
                table: "economy_reward_definitions",
                columns: new[] { "RewardType", "RewardIdPattern" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "economy_reward_definitions");
        }
    }
}

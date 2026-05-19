using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class MakeRewardCatalogDataDrivenAndAdminGrantAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_economy_reward_definitions_type_active",
                table: "economy_reward_definitions");

            migrationBuilder.DropColumn(
                name: "Coins",
                table: "economy_reward_definitions");

            migrationBuilder.DropColumn(
                name: "CoinsPerUnit",
                table: "economy_reward_definitions");

            migrationBuilder.DropColumn(
                name: "MatchStrategy",
                table: "economy_reward_definitions");

            migrationBuilder.DropColumn(
                name: "MaxCoins",
                table: "economy_reward_definitions");

            migrationBuilder.DropColumn(
                name: "MinCoins",
                table: "economy_reward_definitions");

            migrationBuilder.RenameColumn(
                name: "Xp",
                table: "economy_reward_definitions",
                newName: "Priority");

            migrationBuilder.AddColumn<string>(
                name: "EligibilityRuleJson",
                table: "economy_reward_definitions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrantRuleJson",
                table: "economy_reward_definitions",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IneligibilityMessage",
                table: "economy_reward_definitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "admin_economy_reward_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    GrantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EconomyTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Coins = table.Column<int>(type: "integer", nullable: false),
                    Xp = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_economy_reward_grants", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "economy_reward_definitions",
                keyColumn: "Id",
                keyValue: new Guid("2e3d6e31-3f8d-4d60-9266-3cbdb3a34729"),
                columns: new[] { "EligibilityRuleJson", "GrantRuleJson", "IneligibilityMessage", "Priority", "RewardIdPattern" },
                values: new object[] { "{\"type\":\"always\"}", "{\"coins\":{\"type\":\"const\",\"value\":50},\"xp\":{\"type\":\"const\",\"value\":0}}", "Reward is not eligible.", 10, "^generic:onboarding_bonus$" });

            migrationBuilder.UpdateData(
                table: "economy_reward_definitions",
                keyColumn: "Id",
                keyValue: new Guid("7b40d3ba-e74d-4e25-bd84-60d2d645a1c1"),
                columns: new[] { "EligibilityRuleJson", "GrantRuleJson", "IneligibilityMessage", "Priority", "RewardIdPattern" },
                values: new object[] { "{\"type\":\"always\"}", "{\"coins\":{\"type\":\"const\",\"value\":20},\"xp\":{\"type\":\"const\",\"value\":15}}", "Reward is not eligible.", 20, "^daily:(?<slug>.+)$" });

            migrationBuilder.UpdateData(
                table: "economy_reward_definitions",
                keyColumn: "Id",
                keyValue: new Guid("d4e88c31-56c0-494b-9611-271db4f1dcd8"),
                columns: new[] { "EligibilityRuleJson", "GrantRuleJson", "IneligibilityMessage", "Priority", "RewardIdPattern" },
                values: new object[] { "{\"type\":\"always\"}", "{\"coins\":{\"type\":\"const\",\"value\":25},\"xp\":{\"type\":\"const\",\"value\":0}}", "Reward is not eligible.", 10, "^generic:starter_bonus$" });

            migrationBuilder.UpdateData(
                table: "economy_reward_definitions",
                keyColumn: "Id",
                keyValue: new Guid("d9d5e0d8-87fa-4819-be4a-6285c2ef6fc7"),
                columns: new[] { "EligibilityRuleJson", "GrantRuleJson", "IneligibilityMessage", "Priority", "RewardIdPattern" },
                values: new object[] { "{\"type\":\"compare\",\"operator\":\"gte\",\"left\":{\"type\":\"profile\",\"field\":\"level\"},\"right\":{\"type\":\"capture\",\"name\":\"threshold\"}}", "{\"coins\":{\"type\":\"clamp\",\"value\":{\"type\":\"multiply\",\"left\":{\"type\":\"capture\",\"name\":\"threshold\"},\"right\":{\"type\":\"const\",\"value\":10}},\"min\":{\"type\":\"const\",\"value\":10}},\"xp\":{\"type\":\"const\",\"value\":0}}", "Reward is not eligible.", 30, "^level:(?<threshold>[1-9]\\d*)$" });

            migrationBuilder.UpdateData(
                table: "economy_reward_definitions",
                keyColumn: "Id",
                keyValue: new Guid("e1f90a77-eeb8-4fd7-973e-e05449b7678a"),
                columns: new[] { "EligibilityRuleJson", "GrantRuleJson", "IneligibilityMessage", "RewardIdPattern" },
                values: new object[] { "{\"type\":\"always\"}", "{\"coins\":{\"type\":\"const\",\"value\":15},\"xp\":{\"type\":\"const\",\"value\":10}}", "Reward is not eligible.", "^generic:welcome_back$" });

            migrationBuilder.UpdateData(
                table: "economy_reward_definitions",
                keyColumn: "Id",
                keyValue: new Guid("fa5d14d5-7931-4b57-b5d0-442afc4ba26e"),
                columns: new[] { "EligibilityRuleJson", "GrantRuleJson", "IneligibilityMessage", "Priority", "RewardIdPattern" },
                values: new object[] { "{\"type\":\"compare\",\"operator\":\"gte\",\"left\":{\"type\":\"profile\",\"field\":\"streak\"},\"right\":{\"type\":\"capture\",\"name\":\"threshold\"}}", "{\"coins\":{\"type\":\"clamp\",\"value\":{\"type\":\"multiply\",\"left\":{\"type\":\"capture\",\"name\":\"threshold\"},\"right\":{\"type\":\"const\",\"value\":5}},\"min\":{\"type\":\"const\",\"value\":10},\"max\":{\"type\":\"const\",\"value\":500}},\"xp\":{\"type\":\"const\",\"value\":0}}", "Reward is not eligible.", 30, "^streak:(?<threshold>[1-9]\\d*)$" });

            migrationBuilder.CreateIndex(
                name: "IX_economy_reward_definitions_type_active_priority",
                table: "economy_reward_definitions",
                columns: new[] { "RewardType", "IsActive", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_economy_reward_grants_actor_created_at",
                table: "admin_economy_reward_grants",
                columns: new[] { "ActorUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_admin_economy_reward_grants_transaction",
                table: "admin_economy_reward_grants",
                column: "EconomyTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_admin_economy_reward_grants_user_grant",
                table: "admin_economy_reward_grants",
                columns: new[] { "UserId", "GrantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_economy_reward_grants");

            migrationBuilder.DropIndex(
                name: "IX_economy_reward_definitions_type_active_priority",
                table: "economy_reward_definitions");

            migrationBuilder.DropColumn(
                name: "EligibilityRuleJson",
                table: "economy_reward_definitions");

            migrationBuilder.DropColumn(
                name: "GrantRuleJson",
                table: "economy_reward_definitions");

            migrationBuilder.DropColumn(
                name: "IneligibilityMessage",
                table: "economy_reward_definitions");

            migrationBuilder.RenameColumn(
                name: "Priority",
                table: "economy_reward_definitions",
                newName: "Xp");

            migrationBuilder.AddColumn<int>(
                name: "Coins",
                table: "economy_reward_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CoinsPerUnit",
                table: "economy_reward_definitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchStrategy",
                table: "economy_reward_definitions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MaxCoins",
                table: "economy_reward_definitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinCoins",
                table: "economy_reward_definitions",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "economy_reward_definitions",
                keyColumn: "Id",
                keyValue: new Guid("2e3d6e31-3f8d-4d60-9266-3cbdb3a34729"),
                columns: new[] { "Coins", "CoinsPerUnit", "MatchStrategy", "MaxCoins", "MinCoins", "RewardIdPattern", "Xp" },
                values: new object[] { 50, null, "Exact", null, null, "generic:onboarding_bonus", 0 });

            migrationBuilder.UpdateData(
                table: "economy_reward_definitions",
                keyColumn: "Id",
                keyValue: new Guid("7b40d3ba-e74d-4e25-bd84-60d2d645a1c1"),
                columns: new[] { "Coins", "CoinsPerUnit", "MatchStrategy", "MaxCoins", "MinCoins", "RewardIdPattern", "Xp" },
                values: new object[] { 20, null, "PrefixNonEmptySuffix", null, null, "daily:", 15 });

            migrationBuilder.UpdateData(
                table: "economy_reward_definitions",
                keyColumn: "Id",
                keyValue: new Guid("d4e88c31-56c0-494b-9611-271db4f1dcd8"),
                columns: new[] { "Coins", "CoinsPerUnit", "MatchStrategy", "MaxCoins", "MinCoins", "RewardIdPattern", "Xp" },
                values: new object[] { 25, null, "Exact", null, null, "generic:starter_bonus", 0 });

            migrationBuilder.UpdateData(
                table: "economy_reward_definitions",
                keyColumn: "Id",
                keyValue: new Guid("d9d5e0d8-87fa-4819-be4a-6285c2ef6fc7"),
                columns: new[] { "Coins", "CoinsPerUnit", "MatchStrategy", "MaxCoins", "MinCoins", "RewardIdPattern", "Xp" },
                values: new object[] { 0, 10, "NumericSuffixLevelThreshold", null, 10, "level:", 0 });

            migrationBuilder.UpdateData(
                table: "economy_reward_definitions",
                keyColumn: "Id",
                keyValue: new Guid("e1f90a77-eeb8-4fd7-973e-e05449b7678a"),
                columns: new[] { "Coins", "CoinsPerUnit", "MatchStrategy", "MaxCoins", "MinCoins", "RewardIdPattern" },
                values: new object[] { 15, null, "Exact", null, null, "generic:welcome_back" });

            migrationBuilder.UpdateData(
                table: "economy_reward_definitions",
                keyColumn: "Id",
                keyValue: new Guid("fa5d14d5-7931-4b57-b5d0-442afc4ba26e"),
                columns: new[] { "Coins", "CoinsPerUnit", "MatchStrategy", "MaxCoins", "MinCoins", "RewardIdPattern", "Xp" },
                values: new object[] { 0, 5, "NumericSuffixStreakThreshold", 500, 10, "streak:", 0 });

            migrationBuilder.CreateIndex(
                name: "IX_economy_reward_definitions_type_active",
                table: "economy_reward_definitions",
                columns: new[] { "RewardType", "IsActive" });
        }
    }
}

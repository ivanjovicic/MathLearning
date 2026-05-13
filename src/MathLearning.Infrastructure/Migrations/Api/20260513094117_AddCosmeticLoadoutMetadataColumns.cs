using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddCosmeticLoadoutMetadataColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnswerEffectJson",
                table: "user_cosmetic_loadout_projections",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarGearJson",
                table: "user_cosmetic_loadout_projections",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrameJson",
                table: "user_cosmetic_loadout_projections",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileBackgroundJson",
                table: "user_cosmetic_loadout_projections",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrailJson",
                table: "user_cosmetic_loadout_projections",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_cosmetic_loadouts_bg_id",
                table: "user_cosmetic_loadout_projections",
                column: "ProfileBackgroundId");

            migrationBuilder.CreateIndex(
                name: "IX_user_cosmetic_loadouts_frame_id",
                table: "user_cosmetic_loadout_projections",
                column: "AvatarFrameId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_cosmetic_loadouts_bg_id",
                table: "user_cosmetic_loadout_projections");

            migrationBuilder.DropIndex(
                name: "IX_user_cosmetic_loadouts_frame_id",
                table: "user_cosmetic_loadout_projections");

            migrationBuilder.DropColumn(
                name: "AnswerEffectJson",
                table: "user_cosmetic_loadout_projections");

            migrationBuilder.DropColumn(
                name: "AvatarGearJson",
                table: "user_cosmetic_loadout_projections");

            migrationBuilder.DropColumn(
                name: "FrameJson",
                table: "user_cosmetic_loadout_projections");

            migrationBuilder.DropColumn(
                name: "ProfileBackgroundJson",
                table: "user_cosmetic_loadout_projections");

            migrationBuilder.DropColumn(
                name: "TrailJson",
                table: "user_cosmetic_loadout_projections");
        }
    }
}

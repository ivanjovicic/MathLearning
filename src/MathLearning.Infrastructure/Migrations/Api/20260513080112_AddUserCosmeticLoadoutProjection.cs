using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddUserCosmeticLoadoutProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent because some dev databases already contain these columns from older repair/branch drift.
            migrationBuilder.Sql(
                @"ALTER TABLE ""Questions""
ADD COLUMN IF NOT EXISTS ""DeletedAt"" timestamp with time zone;");

            migrationBuilder.Sql(
                @"ALTER TABLE ""Questions""
ADD COLUMN IF NOT EXISTS ""IsDeleted"" boolean NOT NULL DEFAULT FALSE;");

            migrationBuilder.Sql(
                @"ALTER TABLE ""Options""
ADD COLUMN IF NOT EXISTS ""Order"" integer NOT NULL DEFAULT 0;");

            migrationBuilder.Sql(
                @"CREATE TABLE IF NOT EXISTS ""user_cosmetic_loadout_projections"" (
  ""UserId"" character varying(450) NOT NULL,
  ""AvatarFrameId"" integer NULL,
  ""TrailId"" integer NULL,
  ""AvatarGearId"" integer NULL,
  ""AnswerEffectId"" integer NULL,
  ""ProfileBackgroundId"" integer NULL,
  ""RecentRareUnlocksJson"" jsonb NULL,
  ""LoadoutVersion"" bigint NOT NULL,
  ""UpdatedAtUtc"" timestamp with time zone NOT NULL,
  CONSTRAINT ""PK_user_cosmetic_loadout_projections"" PRIMARY KEY (""UserId"")
);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_cosmetic_loadout_projections");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "Options");
        }
    }
}

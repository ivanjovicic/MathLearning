using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MathLearning.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddStreakFreezeToUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StreakFreezeCount",
                table: "UserProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastActivityDay",
                table: "UserProfiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastStreakDay",
                table: "UserProfiles",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StreakFreezeCount",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LastActivityDay",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LastStreakDay",
                table: "UserProfiles");
        }
    }
}

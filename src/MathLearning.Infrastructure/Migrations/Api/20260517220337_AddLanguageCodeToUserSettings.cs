using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddLanguageCodeToUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "user_settings",
                type: "varchar(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "user_settings");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddRefreshTokenSecurityStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "RefreshTokens",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "RefreshTokens" rt
                SET "SecurityStamp" = COALESCE(u."SecurityStamp", '')
                FROM "AspNetUsers" u
                WHERE rt."UserId" = u."Id"
                  AND rt."SecurityStamp" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "RefreshTokens");
        }
    }
}

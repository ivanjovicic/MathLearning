using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddDesignTokenDraftUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_DesignTokenVersion_Draft",
                table: "DesignTokenVersion",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 'Draft'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_DesignTokenVersion_Draft",
                table: "DesignTokenVersion");
        }
    }
}

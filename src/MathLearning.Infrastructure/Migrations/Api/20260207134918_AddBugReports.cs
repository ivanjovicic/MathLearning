using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddBugReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bug_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    UsernameSnapshot = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Screen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StepsToReproduce = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AppVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScreenshotUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Assignee = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bug_reports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_CreatedAt",
                table: "bug_reports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_Severity",
                table: "bug_reports",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_Status",
                table: "bug_reports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_Status_CreatedAt",
                table: "bug_reports",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_User_CreatedAt",
                table: "bug_reports",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_UserId",
                table: "bug_reports",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bug_reports");
        }
    }
}

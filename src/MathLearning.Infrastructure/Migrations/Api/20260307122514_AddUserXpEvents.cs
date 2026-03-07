using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddUserXpEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_xp_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    SchoolId = table.Column<int>(type: "integer", nullable: true),
                    XpDelta = table.Column<int>(type: "integer", nullable: false),
                    ValidatedXpDelta = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ValidationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsSuspicious = table.Column<bool>(type: "boolean", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    AwardedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_xp_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_xp_events_school_awarded_at",
                table: "user_xp_events",
                columns: new[] { "SchoolId", "AwardedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_user_xp_events_user_awarded_at",
                table: "user_xp_events",
                columns: new[] { "UserId", "AwardedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_user_xp_events_validation_awarded_at",
                table: "user_xp_events",
                columns: new[] { "ValidationStatus", "AwardedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_xp_events");
        }
    }
}

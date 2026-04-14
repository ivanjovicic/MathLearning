using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    public partial class AddSyncDeadLetterRedriveLifecycle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRedriveAttemptAtUtc",
                table: "SyncDeadLetter",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAtUtc",
                table: "SyncDeadLetter",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNote",
                table: "SyncDeadLetter",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SyncDeadLetter",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_SyncDeadLetter_Status_LastFailedAtUtc",
                table: "SyncDeadLetter",
                columns: new[] { "Status", "LastFailedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SyncDeadLetter_Status_LastFailedAtUtc",
                table: "SyncDeadLetter");

            migrationBuilder.DropColumn(
                name: "LastRedriveAttemptAtUtc",
                table: "SyncDeadLetter");

            migrationBuilder.DropColumn(
                name: "ResolvedAtUtc",
                table: "SyncDeadLetter");

            migrationBuilder.DropColumn(
                name: "ResolutionNote",
                table: "SyncDeadLetter");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SyncDeadLetter");
        }
    }
}

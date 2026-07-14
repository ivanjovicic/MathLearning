using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddOutboxRetryAndDeadLetter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredUtc",
                table: "Outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptUtc",
                table: "Outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_ProcessedUtc_DeadLetteredUtc_NextAttemptUtc_Occurred~",
                table: "Outbox",
                columns: new[] { "ProcessedUtc", "DeadLetteredUtc", "NextAttemptUtc", "OccurredUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Outbox_ProcessedUtc_DeadLetteredUtc_NextAttemptUtc_Occurred~",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "DeadLetteredUtc",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "NextAttemptUtc",
                table: "Outbox");
        }
    }
}

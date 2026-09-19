using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddIdempotencyLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "economy_transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAtUtc",
                table: "economy_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerToken",
                table: "economy_transactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "cosmetics_idempotency_ledger",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAtUtc",
                table: "cosmetics_idempotency_ledger",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerToken",
                table: "cosmetics_idempotency_ledger",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncEventLog_Status_ReceivedAtUtc_Id",
                table: "SyncEventLog",
                columns: new[] { "Status", "ReceivedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncDeadLetter_Status_LastFailedAtUtc_Id",
                table: "SyncDeadLetter",
                columns: new[] { "Status", "LastFailedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ServerSyncEvent_CreatedAtUtc_Id",
                table: "ServerSyncEvent",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_economy_transactions_pending_lease",
                table: "economy_transactions",
                columns: new[] { "Status", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_cosmetics_idempotency_ledger_pending_lease",
                table: "cosmetics_idempotency_ledger",
                columns: new[] { "Status", "LeaseExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SyncEventLog_Status_ReceivedAtUtc_Id",
                table: "SyncEventLog");

            migrationBuilder.DropIndex(
                name: "IX_SyncDeadLetter_Status_LastFailedAtUtc_Id",
                table: "SyncDeadLetter");

            migrationBuilder.DropIndex(
                name: "IX_ServerSyncEvent_CreatedAtUtc_Id",
                table: "ServerSyncEvent");

            migrationBuilder.DropIndex(
                name: "IX_economy_transactions_pending_lease",
                table: "economy_transactions");

            migrationBuilder.DropIndex(
                name: "IX_cosmetics_idempotency_ledger_pending_lease",
                table: "cosmetics_idempotency_ledger");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "economy_transactions");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAtUtc",
                table: "economy_transactions");

            migrationBuilder.DropColumn(
                name: "OwnerToken",
                table: "economy_transactions");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "cosmetics_idempotency_ledger");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAtUtc",
                table: "cosmetics_idempotency_ledger");

            migrationBuilder.DropColumn(
                name: "OwnerToken",
                table: "cosmetics_idempotency_ledger");
        }
    }
}

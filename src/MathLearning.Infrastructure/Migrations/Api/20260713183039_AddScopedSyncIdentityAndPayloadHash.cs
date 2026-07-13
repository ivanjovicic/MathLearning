using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddScopedSyncIdentityAndPayloadHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_UserAnswers_SyncOperationId",
                table: "UserAnswers");

            migrationBuilder.DropIndex(
                name: "UX_SyncEventLog_Device_Sequence",
                table: "SyncEventLog");

            migrationBuilder.DropIndex(
                name: "UX_SyncEventLog_OperationId",
                table: "SyncEventLog");

            migrationBuilder.DropIndex(
                name: "UX_SyncDeadLetter_OperationId",
                table: "SyncDeadLetter");

            migrationBuilder.AddColumn<string>(
                name: "PayloadHash",
                table: "SyncEventLog",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PayloadHash",
                table: "SyncDeadLetter",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "UX_UserAnswers_User_Device_SyncOperationId",
                table: "UserAnswers",
                columns: new[] { "UserId", "DeviceId", "SyncOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SyncEventLog_User_Device_OperationId",
                table: "SyncEventLog",
                columns: new[] { "UserId", "DeviceId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SyncEventLog_User_Device_Sequence",
                table: "SyncEventLog",
                columns: new[] { "UserId", "DeviceId", "ClientSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SyncDeadLetter_User_Device_OperationId",
                table: "SyncDeadLetter",
                columns: new[] { "UserId", "DeviceId", "OperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_UserAnswers_User_Device_SyncOperationId",
                table: "UserAnswers");

            migrationBuilder.DropIndex(
                name: "UX_SyncEventLog_User_Device_OperationId",
                table: "SyncEventLog");

            migrationBuilder.DropIndex(
                name: "UX_SyncEventLog_User_Device_Sequence",
                table: "SyncEventLog");

            migrationBuilder.DropIndex(
                name: "UX_SyncDeadLetter_User_Device_OperationId",
                table: "SyncDeadLetter");

            migrationBuilder.DropColumn(
                name: "PayloadHash",
                table: "SyncEventLog");

            migrationBuilder.DropColumn(
                name: "PayloadHash",
                table: "SyncDeadLetter");

            migrationBuilder.CreateIndex(
                name: "UX_UserAnswers_SyncOperationId",
                table: "UserAnswers",
                column: "SyncOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SyncEventLog_Device_Sequence",
                table: "SyncEventLog",
                columns: new[] { "DeviceId", "ClientSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SyncEventLog_OperationId",
                table: "SyncEventLog",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SyncDeadLetter_OperationId",
                table: "SyncDeadLetter",
                column: "OperationId",
                unique: true);
        }
    }
}

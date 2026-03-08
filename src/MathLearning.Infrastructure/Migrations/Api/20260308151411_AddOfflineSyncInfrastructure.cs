using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddOfflineSyncInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ClientSequence",
                table: "UserAnswers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                table: "UserAnswers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncOperationId",
                table: "UserAnswers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DesignTokenVersion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BaseWidth = table.Column<int>(type: "integer", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    SourceVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    PublishedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignTokenVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesignTokenVersion_DesignTokenVersion_SourceVersionId",
                        column: x => x.SourceVersionId,
                        principalTable: "DesignTokenVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeviceSyncState",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    LastAcknowledgedEvent = table.Column<long>(type: "bigint", nullable: false),
                    LastProcessedClientSequence = table.Column<long>(type: "bigint", nullable: false),
                    LastSyncTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastBundleVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceSyncState", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerSyncEvent",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AggregateId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerSyncEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncDeadLetter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncEventLogId = table.Column<long>(type: "bigint", nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    OperationType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastFailedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncDeadLetter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AppVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SecretKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncEventLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClientSequence = table.Column<long>(type: "bigint", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncEventLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DesignTokenSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Theme = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CompiledPayloadJson = table.Column<string>(type: "text", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignTokenSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesignTokenSet_DesignTokenVersion_VersionId",
                        column: x => x.VersionId,
                        principalTable: "DesignTokenVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DesignToken",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TokenKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ValueJson = table.Column<string>(type: "text", nullable: false),
                    ValueType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesignToken_DesignTokenSet_TokenSetId",
                        column: x => x.TokenSetId,
                        principalTable: "DesignTokenSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DesignTokenAuditLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TokenSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Theme = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ActorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    BeforeSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    AfterSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignTokenAuditLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesignTokenAuditLog_DesignTokenSet_TokenSetId",
                        column: x => x.TokenSetId,
                        principalTable: "DesignTokenSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DesignTokenAuditLog_DesignTokenVersion_VersionId",
                        column: x => x.VersionId,
                        principalTable: "DesignTokenVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "UX_UserAnswers_SyncOperationId",
                table: "UserAnswers",
                column: "SyncOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_UserAnswers_User_Device_Sequence",
                table: "UserAnswers",
                columns: new[] { "UserId", "DeviceId", "ClientSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_user_xp_events_user_source",
                table: "user_xp_events",
                columns: new[] { "UserId", "SourceType", "SourceId" },
                unique: true,
                filter: "\"SourceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DesignToken_Category_Key",
                table: "DesignToken",
                columns: new[] { "Category", "TokenKey" });

            migrationBuilder.CreateIndex(
                name: "UX_DesignToken_Set_Category_Key",
                table: "DesignToken",
                columns: new[] { "TokenSetId", "Category", "TokenKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DesignTokenAuditLog_CreatedAtUtc",
                table: "DesignTokenAuditLog",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DesignTokenAuditLog_Theme_CreatedAtUtc",
                table: "DesignTokenAuditLog",
                columns: new[] { "Theme", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DesignTokenAuditLog_TokenSetId",
                table: "DesignTokenAuditLog",
                column: "TokenSetId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignTokenAuditLog_Version_CreatedAtUtc",
                table: "DesignTokenAuditLog",
                columns: new[] { "VersionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DesignTokenSet_Theme_UpdatedAtUtc",
                table: "DesignTokenSet",
                columns: new[] { "Theme", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_DesignTokenSet_Version_Theme",
                table: "DesignTokenSet",
                columns: new[] { "VersionId", "Theme" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DesignTokenVersion_SourceVersionId",
                table: "DesignTokenVersion",
                column: "SourceVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignTokenVersion_Status_CreatedAtUtc",
                table: "DesignTokenVersion",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_DesignTokenVersion_Current",
                table: "DesignTokenVersion",
                column: "IsCurrent",
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "UX_DesignTokenVersion_Version",
                table: "DesignTokenVersion",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSyncState_User_LastSync",
                table: "DeviceSyncState",
                columns: new[] { "UserId", "LastSyncTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_DeviceSyncState_DeviceId",
                table: "DeviceSyncState",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerSyncEvent_User_CreatedAtUtc",
                table: "ServerSyncEvent",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServerSyncEvent_User_Id",
                table: "ServerSyncEvent",
                columns: new[] { "UserId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncDeadLetter_User_CreatedAtUtc",
                table: "SyncDeadLetter",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_SyncDeadLetter_OperationId",
                table: "SyncDeadLetter",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncDevices_User_Status",
                table: "SyncDevices",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_SyncDevices_DeviceId",
                table: "SyncDevices",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncEventLog_Status_ReceivedAtUtc",
                table: "SyncEventLog",
                columns: new[] { "Status", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncEventLog_User_Id",
                table: "SyncEventLog",
                columns: new[] { "UserId", "Id" });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DesignToken");

            migrationBuilder.DropTable(
                name: "DesignTokenAuditLog");

            migrationBuilder.DropTable(
                name: "DeviceSyncState");

            migrationBuilder.DropTable(
                name: "ServerSyncEvent");

            migrationBuilder.DropTable(
                name: "SyncDeadLetter");

            migrationBuilder.DropTable(
                name: "SyncDevices");

            migrationBuilder.DropTable(
                name: "SyncEventLog");

            migrationBuilder.DropTable(
                name: "DesignTokenSet");

            migrationBuilder.DropTable(
                name: "DesignTokenVersion");

            migrationBuilder.DropIndex(
                name: "UX_UserAnswers_SyncOperationId",
                table: "UserAnswers");

            migrationBuilder.DropIndex(
                name: "UX_UserAnswers_User_Device_Sequence",
                table: "UserAnswers");

            migrationBuilder.DropIndex(
                name: "UX_user_xp_events_user_source",
                table: "user_xp_events");

            migrationBuilder.DropColumn(
                name: "ClientSequence",
                table: "UserAnswers");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "UserAnswers");

            migrationBuilder.DropColumn(
                name: "SyncOperationId",
                table: "UserAnswers");
        }
    }
}

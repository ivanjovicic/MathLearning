using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddCosmeticEntitlementsAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cosmetic_entitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    EntitlementType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CosmeticItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceRef = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OperationKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsumedOperationType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConsumedOperationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ConsumedIdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cosmetic_entitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cosmetic_entitlements_cosmetic_items_CosmeticItemId",
                        column: x => x.CosmeticItemId,
                        principalTable: "cosmetic_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cosmetic_entitlements_CosmeticItemId",
                table: "cosmetic_entitlements",
                column: "CosmeticItemId");

            migrationBuilder.CreateIndex(
                name: "IX_cosmetic_entitlements_user_source_item_quantity",
                table: "cosmetic_entitlements",
                columns: new[] { "UserId", "SourceType", "SourceRef", "CosmeticItemId", "Quantity" });

            migrationBuilder.CreateIndex(
                name: "IX_cosmetic_entitlements_user_type_consumed",
                table: "cosmetic_entitlements",
                columns: new[] { "UserId", "EntitlementType", "ConsumedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_cosmetic_entitlements_user_operation",
                table: "cosmetic_entitlements",
                columns: new[] { "UserId", "OperationKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cosmetic_entitlements");
        }
    }
}

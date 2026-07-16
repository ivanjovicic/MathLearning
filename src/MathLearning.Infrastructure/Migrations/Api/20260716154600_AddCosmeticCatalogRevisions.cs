using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddCosmeticCatalogRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cosmetic_catalog_revisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RevisionKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AppliedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cosmetic_catalog_revisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cosmetic_catalog_revisions_applied_at",
                table: "cosmetic_catalog_revisions",
                column: "AppliedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_cosmetic_catalog_revisions_key",
                table: "cosmetic_catalog_revisions",
                column: "RevisionKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cosmetic_catalog_revisions");
        }
    }
}

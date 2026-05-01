using MathLearning.Admin.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Admin.Migrations
{
    [DbContext(typeof(AdminDbContext))]
    [Migration("20260501120000_AddDataProtectionKeysTable")]
    public partial class AddDataProtectionKeysTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS "DataProtectionKeys" (
    "Id"           serial          NOT NULL,
    "FriendlyName" text            NULL,
    "Xml"          text            NULL,
    CONSTRAINT "PK_DataProtectionKeys" PRIMARY KEY ("Id")
);
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "DataProtectionKeys";""");
        }
    }
}

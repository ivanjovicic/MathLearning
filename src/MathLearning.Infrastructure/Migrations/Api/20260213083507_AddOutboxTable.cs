using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddOutboxTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "FacultyName" text NULL;
                ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "SchoolName" text NULL;

                CREATE TABLE IF NOT EXISTS "Outbox"
                (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "OccurredUtc" timestamp with time zone NOT NULL,
                    "Type" text NOT NULL,
                    "PayloadJson" text NOT NULL,
                    "ProcessedUtc" timestamp with time zone NULL,
                    "Attempts" integer NOT NULL,
                    "LastError" text NULL
                );

                CREATE INDEX IF NOT EXISTS "IX_Outbox_ProcessedUtc_OccurredUtc"
                    ON "Outbox" ("ProcessedUtc", "OccurredUtc");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Outbox");

            migrationBuilder.DropColumn(
                name: "FacultyName",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "SchoolName",
                table: "UserProfiles");
        }
    }
}

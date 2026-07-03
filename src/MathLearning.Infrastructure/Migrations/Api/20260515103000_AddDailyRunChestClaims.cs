using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api;

[DbContext(typeof(ApiDbContext))]
[Migration("20260515103000_AddDailyRunChestClaims")]
public sealed class AddDailyRunChestClaims : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS daily_run_chest_claims (
                "Id" uuid NOT NULL,
                "UserId" character varying(450) NOT NULL,
                "Day" date NOT NULL,
                "TransactionId" character varying(128) NOT NULL,
                "Xp" integer NOT NULL,
                "Coins" integer NOT NULL,
                "CosmeticFragment" character varying(128) NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_daily_run_chest_claims" PRIMARY KEY ("Id")
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_daily_run_chest_claims_user_day"
                ON daily_run_chest_claims ("UserId", "Day");

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_daily_run_chest_claims_user_transaction"
                ON daily_run_chest_claims ("UserId", "TransactionId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "daily_run_chest_claims");
    }
}

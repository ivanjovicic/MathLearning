using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api;

public partial class AddEconomyTransactionOperationId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "OperationId",
            table: "economy_transactions",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "UX_economy_transactions_user_type_operation",
            table: "economy_transactions",
            columns: new[] { "UserId", "TransactionType", "OperationId" },
            unique: true,
            filter: "\"OperationId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_economy_transactions_user_type_operation",
            table: "economy_transactions");

        migrationBuilder.DropColumn(
            name: "OperationId",
            table: "economy_transactions");
    }
}

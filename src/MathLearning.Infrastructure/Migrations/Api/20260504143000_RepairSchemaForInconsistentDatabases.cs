using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api;

[DbContext(typeof(ApiDbContext))]
[Migration("20260504143000_RepairSchemaForInconsistentDatabases")]
public partial class RepairSchemaForInconsistentDatabases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(ApiSchemaRepairSql.CriticalRepair);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
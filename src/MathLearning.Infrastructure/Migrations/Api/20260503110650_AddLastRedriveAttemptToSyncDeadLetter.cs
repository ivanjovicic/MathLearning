using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api;

[DbContext(typeof(ApiDbContext))]
[Migration("20260503110650_AddLastRedriveAttemptToSyncDeadLetter")]
public partial class AddLastRedriveAttemptToSyncDeadLetter : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
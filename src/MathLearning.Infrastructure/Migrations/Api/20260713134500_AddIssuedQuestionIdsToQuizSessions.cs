using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    [DbContext(typeof(ApiDbContext))]
    [Migration("20260713134500_AddIssuedQuestionIdsToQuizSessions")]
    public partial class AddIssuedQuestionIdsToQuizSessions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IssuedQuestionIdsJson",
                table: "QuizSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IssuedQuestionIdsJson",
                table: "QuizSessions");
        }
    }
}

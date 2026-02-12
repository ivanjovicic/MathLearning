using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddUserQuestionAttemptRateLimiting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserQuestionAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserQuestionAttempts", x => x.Id);
                });

            // 🚀 Performance indexes for rate limiting queries
            migrationBuilder.CreateIndex(
                name: "IX_UserQuestionAttempts_UserId_QuestionId_AttemptedAt",
                table: "UserQuestionAttempts",
                columns: new[] { "UserId", "QuestionId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserQuestionAttempts_AttemptedAt",
                table: "UserQuestionAttempts",
                column: "AttemptedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserQuestionAttempts");
        }
    }
}

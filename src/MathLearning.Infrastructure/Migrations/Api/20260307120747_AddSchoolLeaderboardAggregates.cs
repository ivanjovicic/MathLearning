using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddSchoolLeaderboardAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "school_rank_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolId = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    XpTotal = table.Column<int>(type: "integer", nullable: false),
                    ActiveStudents = table.Column<int>(type: "integer", nullable: false),
                    ParticipationRate = table.Column<decimal>(type: "numeric(8,6)", nullable: false),
                    CompositeScore = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    SnapshotTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_rank_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_school_rank_history_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "school_scores",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolId = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    XpTotal = table.Column<int>(type: "integer", nullable: false),
                    ActiveStudents = table.Column<int>(type: "integer", nullable: false),
                    EligibleStudents = table.Column<int>(type: "integer", nullable: false),
                    AverageXpPerActiveStudent = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ParticipationRate = table.Column<decimal>(type: "numeric(8,6)", nullable: false),
                    CompositeScore = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_scores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_school_scores_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_school_rank_history_period_snapshot_rank",
                table: "school_rank_history",
                columns: new[] { "Period", "PeriodStartUtc", "SnapshotTimeUtc", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_school_rank_history_school_period_snapshot",
                table: "school_rank_history",
                columns: new[] { "SchoolId", "Period", "PeriodStartUtc", "SnapshotTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_school_scores_period_start_rank",
                table: "school_scores",
                columns: new[] { "Period", "PeriodStartUtc", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_school_scores_period_start_score",
                table: "school_scores",
                columns: new[] { "Period", "PeriodStartUtc", "CompositeScore", "SchoolId" });

            migrationBuilder.CreateIndex(
                name: "UX_school_scores_school_period_start",
                table: "school_scores",
                columns: new[] { "SchoolId", "Period", "PeriodStartUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "school_rank_history");

            migrationBuilder.DropTable(
                name: "school_scores");
        }
    }
}

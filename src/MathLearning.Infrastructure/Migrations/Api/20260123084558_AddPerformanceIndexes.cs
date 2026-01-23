using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserQuestionStats_LastAttempt",
                table: "UserQuestionStats",
                column: "LastAttemptAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserQuestionStats_User_LastAttempt",
                table: "UserQuestionStats",
                columns: new[] { "UserId", "LastAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserFriends_FriendId",
                table: "UserFriends",
                column: "FriendId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAnswers_User_Answered",
                table: "UserAnswers",
                columns: new[] { "UserId", "AnsweredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAnswers_User_Correct",
                table: "UserAnswers",
                columns: new[] { "UserId", "IsCorrect" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAnswers_UserId",
                table: "UserAnswers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_Topics_Name",
                table: "Topics",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Subtopics_Topic_Name",
                table: "Subtopics",
                columns: new[] { "TopicId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuizSessions_User_Started",
                table: "QuizSessions",
                columns: new[] { "UserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuizSessions_UserId",
                table: "QuizSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Difficulty",
                table: "Questions",
                column: "Difficulty");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Subtopic_Difficulty",
                table: "Questions",
                columns: new[] { "SubtopicId", "Difficulty" });

            migrationBuilder.CreateIndex(
                name: "IX_Options_IsCorrect",
                table: "Options",
                column: "IsCorrect");

            migrationBuilder.CreateIndex(
                name: "UX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserQuestionStats_LastAttempt",
                table: "UserQuestionStats");

            migrationBuilder.DropIndex(
                name: "IX_UserQuestionStats_User_LastAttempt",
                table: "UserQuestionStats");

            migrationBuilder.DropIndex(
                name: "IX_UserFriends_FriendId",
                table: "UserFriends");

            migrationBuilder.DropIndex(
                name: "IX_UserAnswers_User_Answered",
                table: "UserAnswers");

            migrationBuilder.DropIndex(
                name: "IX_UserAnswers_User_Correct",
                table: "UserAnswers");

            migrationBuilder.DropIndex(
                name: "IX_UserAnswers_UserId",
                table: "UserAnswers");

            migrationBuilder.DropIndex(
                name: "UX_Topics_Name",
                table: "Topics");

            migrationBuilder.DropIndex(
                name: "UX_Subtopics_Topic_Name",
                table: "Subtopics");

            migrationBuilder.DropIndex(
                name: "IX_QuizSessions_User_Started",
                table: "QuizSessions");

            migrationBuilder.DropIndex(
                name: "IX_QuizSessions_UserId",
                table: "QuizSessions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_Difficulty",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_Subtopic_Difficulty",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Options_IsCorrect",
                table: "Options");

            migrationBuilder.DropIndex(
                name: "UX_Categories_Name",
                table: "Categories");
        }
    }
}

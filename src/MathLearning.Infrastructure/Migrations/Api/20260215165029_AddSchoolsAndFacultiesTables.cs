using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MathLearning.Infrastructure.Migrations.Api
{
    /// <inheritdoc />
    public partial class AddSchoolsAndFacultiesTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_name = 'UserProfiles'
    ) THEN
        IF EXISTS (
            SELECT 1
            FROM pg_constraint
            WHERE conname = 'PK_UserProfiles'
              AND conrelid = '"UserProfiles"'::regclass
        ) THEN
            ALTER TABLE "UserProfiles" DROP CONSTRAINT "PK_UserProfiles";
        END IF;
    END IF;
END $$;
DROP INDEX IF EXISTS "UX_UserProfiles_UserId";
ALTER TABLE "UserProfiles" DROP COLUMN IF EXISTS "Id";
ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "DailyXp" integer NOT NULL DEFAULT 0;
ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "FacultyId" integer NULL;
ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "FacultyName" text NULL;
ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "LastXpResetDate" timestamp with time zone NULL;
ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "LeaderboardOptIn" boolean NOT NULL DEFAULT TRUE;
ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "MonthlyXp" integer NOT NULL DEFAULT 0;
ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "SchoolId" integer NULL;
ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "SchoolName" text NULL;
ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "WeeklyXp" integer NOT NULL DEFAULT 0;
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'UserProfiles'
          AND column_name = 'UserId'
          AND udt_name <> 'text'
    ) THEN
        ALTER TABLE "UserProfiles" ALTER COLUMN "UserId" TYPE text USING "UserId"::text;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'UserQuestionStats'
          AND column_name = 'UserId'
          AND udt_name <> 'text'
    ) THEN
        ALTER TABLE "UserQuestionStats" ALTER COLUMN "UserId" TYPE text USING "UserId"::text;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'UserHints'
          AND column_name = 'UserId'
          AND udt_name <> 'text'
    ) THEN
        ALTER TABLE "UserHints" ALTER COLUMN "UserId" TYPE text USING "UserId"::text;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'UserFriends'
          AND column_name = 'UserId'
          AND udt_name <> 'text'
    ) THEN
        ALTER TABLE "UserFriends" ALTER COLUMN "UserId" TYPE text USING "UserId"::text;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'UserFriends'
          AND column_name = 'FriendId'
          AND udt_name <> 'text'
    ) THEN
        ALTER TABLE "UserFriends" ALTER COLUMN "FriendId" TYPE text USING "FriendId"::text;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'UserAnswers'
          AND column_name = 'UserId'
          AND udt_name <> 'text'
    ) THEN
        ALTER TABLE "UserAnswers" ALTER COLUMN "UserId" TYPE text USING "UserId"::text;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'user_settings'
          AND column_name = 'UserId'
          AND udt_name <> 'text'
    ) THEN
        ALTER TABLE "user_settings" ALTER COLUMN "UserId" TYPE text USING "UserId"::text;
    END IF;
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'user_daily_stats'
          AND column_name = 'UserId'
          AND udt_name <> 'text'
    ) THEN
        ALTER TABLE "user_daily_stats" ALTER COLUMN "UserId" TYPE text USING "UserId"::text;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'RefreshTokens'
          AND column_name = 'UserId'
          AND udt_name <> 'text'
    ) THEN
        ALTER TABLE "RefreshTokens" ALTER COLUMN "UserId" TYPE text USING "UserId"::text;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'QuizSessions'
          AND column_name = 'UserId'
          AND udt_name <> 'text'
    ) THEN
        ALTER TABLE "QuizSessions" ALTER COLUMN "UserId" TYPE text USING "UserId"::text;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'question_stats'
          AND column_name = 'UserId'
          AND udt_name <> 'text'
    ) THEN
        ALTER TABLE "question_stats" ALTER COLUMN "UserId" TYPE text USING "UserId"::text;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'bug_reports'
          AND column_name = 'UserId'
          AND udt_name <> 'text'
    ) THEN
        ALTER TABLE "bug_reports" ALTER COLUMN "UserId" TYPE text USING "UserId"::text;
    END IF;
END $$;
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'PK_UserProfiles'
          AND conrelid = '"UserProfiles"'::regclass
    ) THEN
        ALTER TABLE "UserProfiles" ADD CONSTRAINT "PK_UserProfiles" PRIMARY KEY ("UserId");
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS "Faculties"
(
    "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "Name" character varying(200) NOT NULL,
    "University" character varying(200),
    "City" character varying(100),
    "Country" character varying(100),
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS "Schools"
(
    "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "Name" character varying(200) NOT NULL,
    "City" character varying(100),
    "Country" character varying(100),
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS "UserAnswerAudits"
(
    "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "UserId" text NOT NULL,
    "QuestionId" integer NOT NULL,
    "Answer" text NOT NULL,
    "IsCorrect" boolean NOT NULL,
    "AwardedXp" integer NOT NULL,
    "AnsweredAt" timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS "UserQuestionAttempts"
(
    "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "UserId" text NOT NULL,
    "QuestionId" integer NOT NULL,
    "AttemptedAt" timestamp with time zone NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_UserProfiles_Faculty_Leaderboard"
    ON "UserProfiles" ("FacultyId", "LeaderboardOptIn");
CREATE INDEX IF NOT EXISTS "IX_UserProfiles_Leaderboard_DailyXp"
    ON "UserProfiles" ("LeaderboardOptIn", "DailyXp");
CREATE INDEX IF NOT EXISTS "IX_UserProfiles_Leaderboard_MonthlyXp"
    ON "UserProfiles" ("LeaderboardOptIn", "MonthlyXp");
CREATE INDEX IF NOT EXISTS "IX_UserProfiles_Leaderboard_TotalXp"
    ON "UserProfiles" ("LeaderboardOptIn", "Xp");
CREATE INDEX IF NOT EXISTS "IX_UserProfiles_Leaderboard_WeeklyXp"
    ON "UserProfiles" ("LeaderboardOptIn", "WeeklyXp");
CREATE INDEX IF NOT EXISTS "IX_UserProfiles_School_Leaderboard"
    ON "UserProfiles" ("SchoolId", "LeaderboardOptIn");
CREATE INDEX IF NOT EXISTS "IX_Faculties_Name"
    ON "Faculties" ("Name");
CREATE INDEX IF NOT EXISTS "IX_Schools_Name"
    ON "Schools" ("Name");
""");

            migrationBuilder.Sql("""
DELETE FROM "UserProfiles" up
WHERE NOT EXISTS (
    SELECT 1
    FROM "AspNetUsers" u
    WHERE u."Id" = up."UserId"
);
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_UserProfiles_AspNetUsers_UserId'
          AND conrelid = '"UserProfiles"'::regclass
    ) THEN
        ALTER TABLE "UserProfiles"
        ADD CONSTRAINT "FK_UserProfiles_AspNetUsers_UserId"
        FOREIGN KEY ("UserId")
        REFERENCES "AspNetUsers" ("Id")
        ON DELETE CASCADE;
    END IF;
END $$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_AspNetUsers_UserId",
                table: "UserProfiles");

            migrationBuilder.DropTable(
                name: "Faculties");

            migrationBuilder.DropTable(
                name: "Schools");

            migrationBuilder.DropTable(
                name: "UserAnswerAudits");

            migrationBuilder.DropTable(
                name: "UserQuestionAttempts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserProfiles",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_Faculty_Leaderboard",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_Leaderboard_DailyXp",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_Leaderboard_MonthlyXp",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_Leaderboard_TotalXp",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_Leaderboard_WeeklyXp",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_School_Leaderboard",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "DailyXp",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "FacultyId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "FacultyName",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LastXpResetDate",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LeaderboardOptIn",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "MonthlyXp",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "SchoolName",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "WeeklyXp",
                table: "UserProfiles");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "UserQuestionStats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "UserProfiles",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "UserProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "UserHints",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "FriendId",
                table: "UserFriends",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "UserFriends",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "UserAnswers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "user_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "user_daily_stats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "RefreshTokens",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "QuizSessions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "question_stats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "bug_reports",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserProfiles",
                table: "UserProfiles",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "UX_UserProfiles_UserId",
                table: "UserProfiles",
                column: "UserId",
                unique: true);
        }
    }
}

# Investigation: LEADERBOARD_SYSTEM_GUIDE vs in-repo reset and Hangfire jobs

Date: 2026-11-07 (Europe/Belgrade)
Repo: MathLearning @ b8acd2294d9677bc53a1a85acf3406595c1b49ef
Scope: read-only comparison plus this note. No production SQL, no Fly deploy, not issue #47.

This investigation note comparing guide to code looks at three claims.

## 1. "Runs every hour" vs XpResetBackgroundService

`LEADERBOARD_SYSTEM_GUIDE.md` (Automatic XP Reset, and the architecture bullet for XpResetBackgroundService) says the background service "Runs every hour".

`src/MathLearning.Api/Services/XpResetBackgroundService.cs` does call `XpResetProcessor.RunOnceAsync` in a loop, then delays with `XpResetWindow.GetDelayUntilNextUtcBoundary`.

`XpResetWindow.GetDelayUntilNextUtcBoundary` in `src/MathLearning.Api/Services/XpResetProcessor.cs` computes `now.Date.AddDays(1) - now` — that is the remaining time until the next UTC midnight, not a one-hour poll.

Finding: the guide's hourly cadence is inaccurate for the current service. Daily / weekly / monthly *window* rules still exist (midnight, Monday week start, first of month), but the worker sleeps until the next UTC day boundary rather than waking every hour.

## 2. Monday-Sunday week boundary vs StartOfWeekUtc / GetWeekStart

The guide says weekly XP is "this week (Monday-Sunday)" and "Weekly XP resets on Monday".

`XpResetWindow.GetWeekStart` treats Sunday (`DayOfWeek == 0`) as 6 days after Monday and otherwise subtracts `dayOfWeek - 1`, so weeks start Monday UTC.

`SchoolLeaderboardPeriods.StartOfWeekUtc` uses `((int)day.DayOfWeek + 6) % 7` and subtracts that many days, which is the same Monday-start rule.

`tests/MathLearning.Tests/Services/XpResetProcessorTests.cs` includes `Create_MondayUtcBoundaryAdvancesDailyAndWeeklyWindows` (Sunday 2026-07-19 23:59:59Z to Monday 2026-07-20 00:00:00Z advances daily and weekly). That matches the guide's Monday reset claim.

Finding: week-boundary documentation matches XpResetWindow and StartOfWeekUtc.

## 3. LeaderboardJobs Hangfire stubs vs actual registration

`src/MathLearning.Application/Jobs/LeaderboardJobs.cs` defines `DailyRankSnapshotJob`, `WeeklyLeaderboardResetJob`, and `LeaderboardConsistencyJob`. Each method only logs start/complete comments; the Redis/SQL work is not implemented.

`src/MathLearning.Api/Program.cs` registers Hangfire recurring jobs against `IPracticeHangfireJobs`, `ISchoolLeaderboardHangfireJobs` (refresh every 10 minutes, weekly snapshot at minute 0 of every hour, monthly snapshot every 6 hours), and `IAntiCheatHangfireJobs`. It does not register `LeaderboardJobs`.

Finding: `LeaderboardJobs` is leftover stub code and is not the scheduled reset path. User-profile XP reset is owned by `XpResetBackgroundService` / `XpResetProcessor`. School leaderboard maintenance uses `ISchoolLeaderboardHangfireJobs`, not `LeaderboardJobs`.

## Out of scope / not done

- Did not apply SQL from the guide or any production migration.
- Did not deploy to Fly.
- Did not change production issue #47 artifacts.
- Did not implement the stub job bodies (that would be a separate implementation task).

## Suggested follow-up (not applied in this run)

Update `LEADERBOARD_SYSTEM_GUIDE.md` so XpResetBackgroundService is described as sleeping until the next UTC midnight via `GetDelayUntilNextUtcBoundary`, and so readers are not pointed at `LeaderboardJobs` as if those Hangfire stubs were registered.
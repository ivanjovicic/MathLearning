# Background Job Idempotency and Non-Overlap Spec

Last updated: 2026-06-24  
Scope: Hangfire recurring jobs registered in `src/MathLearning.Api/Program.cs`

## Goals

- Recurring jobs must not corrupt data when a previous run is still active or when the same schedule fires twice.
- Business keys and overlap policy must be explicit for operations review.

## Hangfire hosting notes

- Recurring jobs register only when environment is not `Test`, database startup succeeded, and Hangfire is enabled.
- PostgreSQL storage uses `InvisibilityTimeout = 5 minutes` (see `AddBackgroundJobServices`).
- Recurring Hangfire entry points use `[DisableConcurrentExecution]` to prevent overlapping runs of the same recurring job id.

## Recurring job inventory

| Hangfire id | Service method | Cron | Business key | Overlap policy | Idempotency strategy |
|---|---|---|---|---|---|
| `practice-daily-aggregation` | `IPracticeHangfireJobs.DailyAggregationJob` | `0 2 * * *` (02:00 UTC daily) | active user ids with `LastActivityDay >= today-30` | `[DisableConcurrentExecution(6h)]` | Re-run enqueues the same per-user weakness/adaptive refresh jobs; downstream recompute is safe to repeat. |
| `school-leaderboard-refresh` | `ISchoolLeaderboardHangfireJobs.RefreshAllCurrentPeriodsJob` | `*/10 * * * *` | leaderboard period (`day/week/month/all_time`) + `PeriodStartUtc` aggregate rows | `[DisableConcurrentExecution(15m)]` | `RefreshCurrentPeriodAsync` upserts aggregates by `(SchoolId, Period, PeriodStartUtc)` and removes stale schools. |
| `school-leaderboard-weekly-snapshot` | `ISchoolLeaderboardHangfireJobs.CaptureSnapshotJob("week")` | `0 * * * *` | `period + PeriodStartUtc + SnapshotTimeUtc bucket` | `[DisableConcurrentExecution(30m)]` | Skips capture when any history row exists for the same period bucket within the last 20 minutes. |
| `school-leaderboard-monthly-snapshot` | `ISchoolLeaderboardHangfireJobs.CaptureSnapshotJob("month")` | `15 */6 * * *` | `period + PeriodStartUtc + SnapshotTimeUtc bucket` | `[DisableConcurrentExecution(30m)]` | Same 20-minute snapshot dedupe window as weekly snapshots. |
| `anti-cheat-ml-review-sweep` | `IAntiCheatHangfireJobs.RunMlReviewSweepJob(0)` | `*/5 * * * *` | `AnswerPatternDetectionLog.Id` ML review claim | `[DisableConcurrentExecution(10m)]` + atomic `ExecuteUpdate` claim | Each detection is claimed once via conditional status transition (`queued/failed` → `processing`). |

## On-demand / enqueued jobs (not recurring)

| Trigger | Job | Idempotency |
|---|---|---|
| Practice session complete | `RecomputeWeaknessForUserJob`, `RefreshAdaptivePathJob`, `GenerateRecommendationsJob` | Safe to repeat; refreshes derived projections/cache. |
| Admin anti-cheat API | `RunMlReviewForDetectionJob(id)` | Uses the same atomic ML review claim as the sweep. |

## Follow-up migration prompts

- `school_rank_history` currently dedupes by time window only. A unique index on `(Period, PeriodStartUtc, SnapshotTimeUtc bucket, SchoolId)` is optional hardening if hourly snapshots must be strictly single-row per bucket.
- No migration required for anti-cheat ML claim hardening (conditional update only).

## Validation tests

- `PracticeHangfireJobsTests` — daily aggregation re-run on the same active-user set.
- `SchoolLeaderboardSnapshotIdempotencyTests` — duplicate snapshot capture within 20 minutes.
- `AnswerPatternAntiCheatServiceTests` — ML review replay does not increment attempts; production claim uses atomic `ExecuteUpdate`.

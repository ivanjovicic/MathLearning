using MathLearning.Application.DTOs.Progress;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Idempotency;
using MathLearning.Infrastructure.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MathLearning.Api.Endpoints;

public static class ProgressEndpoints
{
    public static void MapProgressEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/progress")
                       .RequireAuthorization()
                       .WithTags("Progress");

        group.MapGet("/overview", async (
            ApiDbContext db,
            ICosmeticRewardService cosmeticRewardService,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var aggregate = await db.UserQuestionStats
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalAttempts = g.Sum(x => x.Attempts),
                    TotalCorrect = g.Sum(x => x.CorrectAttempts)
                })
                .FirstOrDefaultAsync();

            int totalAttempts = aggregate?.TotalAttempts ?? 0;
            int totalCorrect = aggregate?.TotalCorrect ?? 0;

            double accuracy = totalAttempts == 0
                ? 0
                : Math.Round((double)totalCorrect / totalAttempts * 100, 2);

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            StreakRollEventDto? streakEvent = null;

            if (profile != null)
            {
                var roll = StreakRoller.Apply(profile, today);
                if (roll != null)
                {
                    streakEvent = new StreakRollEventDto(
                        roll.Type,
                        roll.MissedDays,
                        roll.FreezesUsed,
                        roll.StreakBefore,
                        roll.StreakAfter
                    );
                    await db.SaveChangesAsync();
                    await cosmeticRewardService.ProcessProgressRewardsAsync(userId, ctx.RequestAborted);
                }
            }

            int streak = profile?.Streak ?? 0;

            return Results.Ok(new ProgressOverviewDto(
                totalAttempts,
                accuracy,
                streak,
                StreakFreezeCount: profile?.StreakFreezeCount ?? 0,
                LastStreakDay: profile?.LastStreakDay,
                LastActivityDay: profile?.LastActivityDay,
                StreakEvent: streakEvent
            ));
        });

        group.MapGet("/weak-areas", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var weakAreas = await (
                from q in db.Questions.AsNoTracking()
                join s in db.UserQuestionStats.AsNoTracking()
                    .Where(x => x.UserId == userId)
                    on q.Id equals s.QuestionId
                join st in db.Subtopics.AsNoTracking()
                    on q.SubtopicId equals st.Id
                group new { s, st } by new { st.Id, st.Name } into g
                let attempts = g.Sum(x => x.s.Attempts)
                let correct = g.Sum(x => x.s.CorrectAttempts)
                where attempts >= 5
                select new WeakAreaDto(
                    g.Key.Id,
                    g.Key.Name,
                    Math.Round((double)correct / attempts * 100, 2)
                )
            )
            .OrderBy(x => x.Accuracy)
            .Take(5)
            .ToListAsync();

            return Results.Ok(weakAreas);
        });

        group.MapGet("/topics", async (
            ApiDbContext db,
            HttpContext ctx) =>
            await GetTopicProgressAsync(db, ctx));

        app.MapGet("/api/topics/progress", async (
            ApiDbContext db,
            HttpContext ctx) =>
            await GetTopicProgressAsync(db, ctx))
            .RequireAuthorization()
            .WithTags("Progress")
            .WithName("GetTopicProgressLegacyAlias");

        group.MapPost("/sync", async (
            ProgressSyncRequestDto request,
            ApiDbContext db,
            ICosmeticRewardService cosmeticRewardService,
            IIdempotencyLedgerService idempotencyService,
            IOptions<SyncOptions> syncOptions,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            if (request.Completed is not null && request.QuizOperationIds is null && request.PracticeSessionIds is null)
            {
                return ProgressEndpointHelpers.CompatibilityErrorResponse();
            }

            if (!ProgressEndpointHelpers.TryResolveProgressSyncKeys(request, out var operationId, out var idempotencyKey))
            {
                return ProgressEndpointHelpers.CompatibilityErrorResponse();
            }

            var deviceId = request.DeviceId?.Trim();
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return Results.BadRequest(new
                {
                    errorCode = "progress_sync_device_required",
                    message = "DeviceId is required for progress sync."
                });
            }

            var quizOperationIds = NormalizeGuids(request.QuizOperationIds);
            var practiceSessionIds = NormalizeGuids(request.PracticeSessionIds);
            if (quizOperationIds.Count == 0 && practiceSessionIds.Count == 0)
            {
                return Results.BadRequest(new
                {
                    errorCode = "progress_sync_evidence_required",
                    message = "At least one settled quiz or practice reference is required."
                });
            }

            var device = await db.SyncDevices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == userId &&
                         x.DeviceId == deviceId &&
                         x.Status == SyncDeviceStatuses.Active,
                    ct);
            if (device is null)
            {
                return Results.BadRequest(new
                {
                    errorCode = "progress_sync_device_not_registered",
                    message = "Active sync device not found for progress settlement."
                });
            }

            var answerEvidence = quizOperationIds.Count == 0
                ? new List<QuizEvidenceRow>()
                : await db.UserAnswers
                    .AsNoTracking()
                    .Where(x =>
                        x.UserId == userId &&
                        x.DeviceId == deviceId &&
                        x.SyncOperationId.HasValue &&
                        quizOperationIds.Contains(x.SyncOperationId.Value))
                    .Select(x => new QuizEvidenceRow(
                        x.SyncOperationId!.Value,
                        x.AnsweredAt))
                    .ToListAsync(ct);

            if (answerEvidence.Count != quizOperationIds.Count)
            {
                return Results.BadRequest(new
                {
                    errorCode = "progress_sync_evidence_scope_mismatch",
                    message = "One or more quiz evidence references do not belong to the authenticated user/device or are not settled."
                });
            }

            var answerLogStatuses = quizOperationIds.Count == 0
                ? new List<SyncEvidenceRow>()
                : await db.SyncEventLogs
                    .AsNoTracking()
                    .Where(x =>
                        x.UserId == userId &&
                        x.DeviceId == deviceId &&
                        quizOperationIds.Contains(x.OperationId))
                    .Select(x => new SyncEvidenceRow(
                        x.OperationId,
                        x.Status))
                    .ToListAsync(ct);

            if (answerLogStatuses.Count != quizOperationIds.Count ||
                answerLogStatuses.Any(x => !string.Equals(x.Status, SyncEventStatuses.Processed, StringComparison.Ordinal)))
            {
                return Results.BadRequest(new
                {
                    errorCode = "progress_sync_evidence_unsettled",
                    message = "Quiz evidence must reference settled server-synced operations."
                });
            }

            var practiceEvidence = practiceSessionIds.Count == 0
                ? new List<PracticeEvidenceRow>()
                : await db.PracticeSessions
                    .AsNoTracking()
                    .Where(x =>
                        x.UserId == userId &&
                        practiceSessionIds.Contains(x.Id))
                    .Select(x => new PracticeEvidenceRow(
                        x.Id,
                        x.Status,
                        x.CompletedAt))
                    .ToListAsync(ct);

            if (practiceEvidence.Count != practiceSessionIds.Count ||
                practiceEvidence.Any(x => !string.Equals(x.Status, PracticeSessionStatuses.Completed, StringComparison.Ordinal) || x.CompletedAt is null))
            {
                return Results.BadRequest(new
                {
                    errorCode = "progress_sync_practice_scope_mismatch",
                    message = "Practice evidence must belong to the authenticated user and be completed."
                });
            }

            var evidenceDays = new HashSet<DateOnly>();
            foreach (var item in answerEvidence)
            {
                evidenceDays.Add(DateOnly.FromDateTime(item.AnsweredAt));
            }

            foreach (var item in practiceEvidence)
            {
                evidenceDays.Add(DateOnly.FromDateTime(item.CompletedAt!.Value));
            }

            if (evidenceDays.Count == 0)
            {
                return Results.BadRequest(new
                {
                    errorCode = "progress_sync_evidence_required",
                    message = "At least one settled evidence record is required."
                });
            }

            if (evidenceDays.Count > 1)
            {
                return Results.BadRequest(new
                {
                    errorCode = "progress_sync_day_mismatch",
                    message = "All evidence references must resolve to the same UTC day."
                });
            }

            var evidenceDay = evidenceDays.Single();
            var day = request.Day ?? evidenceDay;
            if (request.Day is not null && request.Day.Value != evidenceDay)
            {
                return Results.BadRequest(new
                {
                    errorCode = "progress_sync_day_mismatch",
                    message = "Requested day does not match the resolved evidence day."
                });
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (day > today)
            {
                return Results.BadRequest(new
                {
                    errorCode = "progress_sync_future_date",
                    message = "Progress sync day cannot be in the future."
                });
            }

            var maxOfflineWindowDays = Math.Max(0, syncOptions.Value.ProgressSyncMaxOfflineWindowDays);
            if (today.DayNumber - day.DayNumber > maxOfflineWindowDays)
            {
                return Results.BadRequest(new
                {
                    errorCode = "progress_sync_out_of_window",
                    message = $"Progress sync day is older than the configured offline window of {maxOfflineWindowDays} days."
                });
            }

            var canonicalPayload = ProgressEndpointHelpers.BuildProgressSyncPayload(
                deviceId,
                day,
                quizOperationIds,
                practiceSessionIds);

            var useTransaction = db.Database.IsRelational();
            await using var tx = useTransaction
                ? await db.Database.BeginTransactionAsync(ct)
                : null;

            try
            {
                var begin = await idempotencyService.BeginOrGetExistingAsync(
                    userId,
                    ProgressEndpointHelpers.ProgressSyncOperationType,
                    operationId,
                    idempotencyKey,
                    ProgressEndpointHelpers.ProgressSyncEndpoint,
                    canonicalPayload,
                    ct);

                var idempotentDecision = ProgressEndpointHelpers.HandleIdempotentDecision(begin);
                if (idempotentDecision is not null)
                {
                    return idempotentDecision;
                }

                var profile = await EnsureUserProfileAsync(db, userId, ct);
                var dailyStat = await GetOrCreateDailyStatAsync(db, userId, day, ct);
                var wasAlreadyCompleted = dailyStat.Completed;
                var nowUtc = DateTime.UtcNow;

                if (!wasAlreadyCompleted)
                {
                    dailyStat.Completed = true;
                    profile.LastActivityDay = day;
                    var roll = StreakRoller.Apply(profile, day);
                    if (roll is not null)
                    {
                        profile.LastActivityDay = day;
                    }

                    if (profile.LastStreakDay != day)
                    {
                        profile.Streak += 1;
                        profile.LastStreakDay = day;
                    }

                    profile.UpdatedAt = nowUtc;
                    await db.SaveChangesAsync(ct);
                    await cosmeticRewardService.ProcessProgressRewardsAsync(userId, ct);
                }

                var responseBody = new ProgressSyncResponseDto(
                    day,
                    Completed: true,
                    SettledEvidenceCount: quizOperationIds.Count + practiceSessionIds.Count,
                    DailyStreak: profile.Streak,
                    StreakFreezeCount: profile.StreakFreezeCount,
                    AlreadyProcessed: wasAlreadyCompleted);

                await idempotencyService.CompleteAsync(
                    begin.LedgerId,
                    responseBody,
                    StatusCodes.Status200OK,
                    ct);

                if (wasAlreadyCompleted)
                {
                    await db.SaveChangesAsync(ct);
                }

                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }

                return Results.Ok(responseBody);
            }
            catch (IdempotencyLedgerConflictException ex)
            {
                return ProgressEndpointHelpers.ConflictResult(ex.OperationId, ex.IdempotencyKey);
            }
        });
    }

    private static async Task<IResult> GetTopicProgressAsync(
        ApiDbContext db,
        HttpContext ctx)
    {
        string userId = ctx.User.FindFirst("userId")!.Value;

        var orderedTopics = await db.Topics
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .ToListAsync();

        var result = new List<TopicProgressDto>();

        for (int i = 0; i < orderedTopics.Count; i++)
        {
            var topic = orderedTopics[i];
            var stats = await (
                from q in db.Questions.AsNoTracking()
                join st in db.Subtopics.AsNoTracking()
                    on q.SubtopicId equals st.Id
                join s in db.UserQuestionStats.AsNoTracking()
                    .Where(x => x.UserId == userId)
                    on q.Id equals s.QuestionId
                where st.TopicId == topic.Id
                select new { s.Attempts, s.CorrectAttempts }
            ).ToListAsync();

            int attempts = stats.Sum(x => x.Attempts);
            int correct = stats.Sum(x => x.CorrectAttempts);
            double accuracy = attempts == 0 ? 0 : (double)correct / attempts * 100;
            bool unlocked = i == 0;

            if (!unlocked)
            {
                var previousTopic = orderedTopics[i - 1];
                var previousStats = await (
                    from q in db.Questions.AsNoTracking()
                    join st in db.Subtopics.AsNoTracking()
                        on q.SubtopicId equals st.Id
                    join s in db.UserQuestionStats.AsNoTracking()
                        .Where(x => x.UserId == userId)
                        on q.Id equals s.QuestionId
                    where st.TopicId == previousTopic.Id
                    select new { s.Attempts, s.CorrectAttempts }
                ).ToListAsync();

                int prevAttempts = previousStats.Sum(x => x.Attempts);
                int prevCorrect = previousStats.Sum(x => x.CorrectAttempts);
                double prevAccuracy = prevAttempts == 0 ? 0 : (double)prevCorrect / prevAttempts * 100;
                unlocked = prevAccuracy >= 60.0;
            }

            result.Add(new TopicProgressDto(
                topic.Id,
                topic.Name,
                Math.Round(accuracy, 2),
                unlocked
            ));
        }

        return Results.Ok(result);
    }

    private static async Task<UserProfile> EnsureUserProfileAsync(
        ApiDbContext db,
        string userId,
        CancellationToken ct)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (profile is not null)
        {
            return profile;
        }

        profile = new UserProfile
        {
            UserId = userId,
            Username = userId,
            DisplayName = userId,
            Coins = 100,
            Level = 1,
            Xp = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.UserProfiles.Add(profile);
        return profile;
    }

    private static async Task<UserDailyStat> GetOrCreateDailyStatAsync(
        ApiDbContext db,
        string userId,
        DateOnly day,
        CancellationToken ct)
    {
        var stat = await db.UserDailyStats
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Day == day, ct);

        if (stat is not null)
        {
            return stat;
        }

        stat = new UserDailyStat
        {
            UserId = userId,
            Day = day,
            Completed = false
        };
        db.UserDailyStats.Add(stat);
        return stat;
    }

    private static IReadOnlyList<Guid> NormalizeGuids(IReadOnlyList<Guid>? values)
        => values is null
            ? Array.Empty<Guid>()
            : values
                .Where(x => x != Guid.Empty)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

    private sealed record QuizEvidenceRow(Guid SyncOperationId, DateTime AnsweredAt);
    private sealed record SyncEvidenceRow(Guid OperationId, string Status);
    private sealed record PracticeEvidenceRow(Guid Id, string Status, DateTime? CompletedAt);
}

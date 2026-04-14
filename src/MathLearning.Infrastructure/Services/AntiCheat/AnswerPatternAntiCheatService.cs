using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MathLearning.Application.DTOs.AntiCheat;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MathLearning.Infrastructure.Services.AntiCheat;

public sealed class AntiCheatOptions
{
    public int HistoryLookbackMinutes { get; set; } = 30;
    public int MaxHistoryEvents { get; set; } = 40;
    public int RapidCorrectBurstThreshold { get; set; } = 8;
    public int FastResponseThresholdMs { get; set; } = 2200;
    public int LowVarianceSampleSize { get; set; } = 8;
    public int MaxResponseStdDevMs { get; set; } = 350;
    public int MinAccuracySampleSize { get; set; } = 10;
    public double MinHighAccuracy { get; set; } = 0.90d;
    public int MaxAverageResponseMs { get; set; } = 2500;
    public int RepeatedAnswerThreshold { get; set; } = 6;
    public int RegularCadenceSampleSize { get; set; } = 6;
    public int RegularCadenceStdDevMs { get; set; } = 400;
    public int DetectionThreshold { get; set; } = 50;
    public string PromptVersion { get; set; } = "anti_cheat_answer_v1";
    public bool EnableMlReviewQueue { get; set; } = true;
    public int MlReviewBatchSize { get; set; } = 25;
    public string MlReviewModelName { get; set; } = "local-heuristic-review-v1";
    public int MlReviewMaxAttempts { get; set; } = 5;
}

internal sealed record HistoricalAnswerSignal(
    int QuestionId,
    int? TopicId,
    int? SubtopicId,
    bool IsCorrect,
    int ResponseTimeMs,
    DateTime AnsweredAtUtc,
    string? AnswerFingerprint,
    double? Confidence,
    string SourceType);

public sealed class AntiCheatMlPromptBuilder : IAntiCheatMlPromptBuilder
{
    public AntiCheatMlPromptDto BuildPrompt(
        AntiCheatAnswerObservationInput input,
        AntiCheatDetectionResultDto result,
        IReadOnlyDictionary<string, object?> featureSnapshot)
    {
        const string systemPrompt =
            "You are an anti-cheat reviewer for a math learning platform. " +
            "Analyze only the provided behavioral telemetry. " +
            "Do not infer identity, demographics, or intent beyond the evidence. " +
            "Focus on timing, cadence regularity, accuracy at improbable speed, repeated answer fingerprints, " +
            "and burst behavior. Respond with strict JSON: " +
            "{\"classification\":\"likely_human|needs_review|likely_bot\",\"confidence\":0.0,\"reasons\":[\"...\"],\"recommendedAction\":\"none|review|rate_limit|temporary_hold\"}.";

        var payloadJson = JsonSerializer.Serialize(new
        {
            currentObservation = new
            {
                input.SourceType,
                input.QuestionId,
                input.TopicId,
                input.SubtopicId,
                input.SessionId,
                input.DeviceId,
                input.ClientSequence,
                input.IsCorrect,
                input.ResponseTimeMs,
                input.Confidence,
                input.AnsweredAtUtc
            },
            detection = new
            {
                result.RiskScore,
                result.Severity,
                result.Decision,
                result.ReasonSummary,
                result.Signals
            },
            features = featureSnapshot
        });

        var userPrompt =
            "Review this answer-behavior event and classify it. " +
            "Use only the JSON below.\n" +
            payloadJson;

        return new AntiCheatMlPromptDto(
            result.Prompt.PromptVersion,
            systemPrompt,
            userPrompt,
            payloadJson);
    }
}

public sealed class AnswerPatternAntiCheatService : IAnswerPatternAntiCheatService, IAntiCheatAdminService, IAntiCheatMlReviewService
{
    private const string DetectionEngine = "heuristic_answer_pattern_v1";
    private readonly ApiDbContext db;
    private readonly AntiCheatOptions options;
    private readonly IAntiCheatMlPromptBuilder promptBuilder;
    private readonly ILogger<AnswerPatternAntiCheatService> logger;

    public AnswerPatternAntiCheatService(
        ApiDbContext db,
        IOptions<AntiCheatOptions> options,
        IAntiCheatMlPromptBuilder promptBuilder,
        ILogger<AnswerPatternAntiCheatService> logger)
    {
        this.db = db;
        this.options = options.Value;
        this.promptBuilder = promptBuilder;
        this.logger = logger;
    }

    public async Task<AntiCheatDetectionResultDto> EvaluateAndTrackAsync(
        AntiCheatAnswerObservationInput input,
        CancellationToken cancellationToken = default)
    {
        var results = await EvaluateAndTrackBatchAsync([input], cancellationToken);
        return results[0];
    }

    public async Task<IReadOnlyList<AntiCheatDetectionResultDto>> EvaluateAndTrackBatchAsync(
        IReadOnlyList<AntiCheatAnswerObservationInput> inputs,
        CancellationToken cancellationToken = default)
    {
        if (inputs.Count == 0)
        {
            return [];
        }

        var orderedInputs = inputs
            .OrderBy(x => x.AnsweredAtUtc)
            .ThenBy(x => x.ClientSequence ?? long.MaxValue)
            .ToList();

        var lookbackStart = orderedInputs[0].AnsweredAtUtc.AddMinutes(-Math.Max(1, options.HistoryLookbackMinutes));
        var history = await LoadHistoryAsync(orderedInputs[0].UserId, lookbackStart, cancellationToken);
        var results = new List<AntiCheatDetectionResultDto>(orderedInputs.Count);

        foreach (var input in orderedInputs)
        {
            var featureSnapshot = BuildFeatureSnapshot(history, input);
            var result = BuildDetectionResult(input, featureSnapshot);
            var prompt = promptBuilder.BuildPrompt(input, result, featureSnapshot);
            var finalResult = result with { Prompt = prompt };
            results.Add(finalResult);

            if (finalResult.IsSuspicious)
            {
                db.AnswerPatternDetectionLogs.Add(new AnswerPatternDetectionLog
                {
                    UserId = input.UserId,
                    SourceType = input.SourceType,
                    QuestionId = input.QuestionId,
                    TopicId = input.TopicId,
                    SubtopicId = input.SubtopicId,
                    SessionId = input.SessionId,
                    DeviceId = input.DeviceId,
                    ClientSequence = input.ClientSequence,
                    AnsweredAtUtc = input.AnsweredAtUtc,
                    ResponseTimeMs = Math.Max(0, input.ResponseTimeMs),
                    IsCorrect = input.IsCorrect,
                    Confidence = input.Confidence,
                    AnswerLength = input.Answer?.Length ?? 0,
                    AnswerFingerprint = Fingerprint(input.Answer),
                    RiskScore = finalResult.RiskScore,
                    Severity = finalResult.Severity,
                    Decision = finalResult.Decision,
                    ReasonSummary = finalResult.ReasonSummary,
                    SignalsJson = JsonSerializer.Serialize(finalResult.Signals),
                    PromptVersion = prompt.PromptVersion,
                    PromptPayloadJson = JsonSerializer.Serialize(prompt),
                    DetectionEngine = DetectionEngine,
                    ReviewStatus = AntiCheatReviewStatuses.Pending,
                    MlReviewStatus = options.EnableMlReviewQueue &&
                                     (finalResult.Severity == AntiCheatDetectionSeverities.High ||
                                      finalResult.Severity == AntiCheatDetectionSeverities.Critical)
                        ? AntiCheatMlReviewStatuses.Queued
                        : AntiCheatMlReviewStatuses.NotRequested,
                    DetectedAtUtc = DateTime.UtcNow
                });

                logger.LogWarning(
                    "Answer anti-cheat flagged. UserId={UserId} SourceType={SourceType} QuestionId={QuestionId} RiskScore={RiskScore} Severity={Severity} Signals={Signals}",
                    input.UserId,
                    input.SourceType,
                    input.QuestionId,
                    finalResult.RiskScore,
                    finalResult.Severity,
                    string.Join(", ", finalResult.Signals));
            }

            history.Add(new HistoricalAnswerSignal(
                input.QuestionId,
                input.TopicId,
                input.SubtopicId,
                input.IsCorrect,
                Math.Max(0, input.ResponseTimeMs),
                input.AnsweredAtUtc,
                Fingerprint(input.Answer),
                input.Confidence,
                input.SourceType));
        }

        return results;
    }

    public async Task<AntiCheatOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var sinceUtc = DateTime.UtcNow.AddHours(-24);
        var query = db.AnswerPatternDetectionLogs.AsNoTracking();

        var pendingCount = await query.LongCountAsync(x => x.ReviewStatus == AntiCheatReviewStatuses.Pending, cancellationToken);
        var highSeverityCount = await query.LongCountAsync(x => x.Severity == AntiCheatDetectionSeverities.High, cancellationToken);
        var criticalSeverityCount = await query.LongCountAsync(x => x.Severity == AntiCheatDetectionSeverities.Critical, cancellationToken);
        var confirmedCount = await query.LongCountAsync(x => x.ReviewStatus == AntiCheatReviewStatuses.Confirmed, cancellationToken);
        var falsePositiveCount = await query.LongCountAsync(x => x.ReviewStatus == AntiCheatReviewStatuses.FalsePositive, cancellationToken);
        var last24HoursCount = await query.LongCountAsync(x => x.DetectedAtUtc >= sinceUtc, cancellationToken);
        var mlQueuedCount = await query.LongCountAsync(x => x.MlReviewStatus == AntiCheatMlReviewStatuses.Queued || x.MlReviewStatus == AntiCheatMlReviewStatuses.Processing, cancellationToken);
        var mlFailedCount = await query.LongCountAsync(x => x.MlReviewStatus == AntiCheatMlReviewStatuses.Failed, cancellationToken);
        var mlCompletedCount = await query.LongCountAsync(x => x.MlReviewStatus == AntiCheatMlReviewStatuses.Completed, cancellationToken);

        return new AntiCheatOverviewDto(
            pendingCount,
            highSeverityCount,
            criticalSeverityCount,
            confirmedCount,
            falsePositiveCount,
            last24HoursCount,
            mlQueuedCount,
            mlFailedCount,
            mlCompletedCount);
    }

    public async Task<IReadOnlyList<AntiCheatDetectionItemDto>> GetDetectionsAsync(
        int take,
        string? reviewStatus,
        string? severity,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        var query = db.AnswerPatternDetectionLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(reviewStatus))
        {
            query = query.Where(x => x.ReviewStatus == reviewStatus.Trim().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            query = query.Where(x => x.Severity == severity.Trim().ToLowerInvariant());
        }

        return await query
            .OrderByDescending(x => x.DetectedAtUtc)
            .Take(take)
            .Select(x => new AntiCheatDetectionItemDto(
                x.Id,
                x.UserId,
                x.SourceType,
                x.QuestionId,
                x.TopicId,
                x.SubtopicId,
                x.SessionId,
                x.DeviceId,
                x.ClientSequence,
                x.AnsweredAtUtc,
                x.ResponseTimeMs,
                x.IsCorrect,
                x.Confidence,
                x.RiskScore,
                x.Severity,
                x.Decision,
                x.ReasonSummary,
                x.ReviewStatus,
                x.DetectedAtUtc,
                x.ReviewedAtUtc,
                x.SignalsJson,
                x.PromptVersion,
                x.MlReviewStatus,
                x.MlReviewAttempts,
                x.MlModelName,
                x.MlReviewedAtUtc,
                x.MlLastError,
                x.MlReviewOutputJson))
            .ToListAsync(cancellationToken);
    }

    public async Task<AntiCheatDetectionItemDto> ReviewDetectionAsync(
        Guid id,
        string reviewStatus,
        string? notes,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedStatus = (reviewStatus ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedStatus is not (
            AntiCheatReviewStatuses.Pending or
            AntiCheatReviewStatuses.Confirmed or
            AntiCheatReviewStatuses.FalsePositive or
            AntiCheatReviewStatuses.Ignored))
        {
            throw new InvalidOperationException("Invalid anti-cheat review status.");
        }

        var entity = await db.AnswerPatternDetectionLogs
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Anti-cheat detection '{id}' was not found.");

        entity.ReviewStatus = normalizedStatus;
        entity.ReviewNotes = notes?.Trim();
        entity.ReviewedByUserId = actorUserId;
        entity.ReviewedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return new AntiCheatDetectionItemDto(
            entity.Id,
            entity.UserId,
            entity.SourceType,
            entity.QuestionId,
            entity.TopicId,
            entity.SubtopicId,
            entity.SessionId,
            entity.DeviceId,
            entity.ClientSequence,
            entity.AnsweredAtUtc,
            entity.ResponseTimeMs,
            entity.IsCorrect,
            entity.Confidence,
            entity.RiskScore,
            entity.Severity,
            entity.Decision,
            entity.ReasonSummary,
            entity.ReviewStatus,
            entity.DetectedAtUtc,
            entity.ReviewedAtUtc,
            entity.SignalsJson,
            entity.PromptVersion,
            entity.MlReviewStatus,
            entity.MlReviewAttempts,
            entity.MlModelName,
            entity.MlReviewedAtUtc,
            entity.MlLastError,
            entity.MlReviewOutputJson);
    }

    public async Task<AntiCheatDetectionItemDto> TriggerMlReviewAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => await ProcessReviewAsync(id, cancellationToken);

    public async Task<int> RunMlReviewSweepAsync(
        int take,
        CancellationToken cancellationToken = default)
        => await ProcessPendingReviewsAsync(take, cancellationToken);

    public async Task<int> ProcessPendingReviewsAsync(int take, CancellationToken cancellationToken = default)
    {
        var effectiveTake = Math.Clamp(take <= 0 ? options.MlReviewBatchSize : take, 1, 200);
        var ids = await db.AnswerPatternDetectionLogs
            .Where(x =>
                x.ReviewStatus == AntiCheatReviewStatuses.Pending &&
                (x.MlReviewStatus == AntiCheatMlReviewStatuses.Queued ||
                 x.MlReviewStatus == AntiCheatMlReviewStatuses.Failed) &&
                x.MlReviewAttempts < options.MlReviewMaxAttempts)
            .OrderByDescending(x => x.Severity == AntiCheatDetectionSeverities.Critical)
            .ThenByDescending(x => x.RiskScore)
            .ThenBy(x => x.DetectedAtUtc)
            .Select(x => x.Id)
            .Take(effectiveTake)
            .ToListAsync(cancellationToken);

        var processedCount = 0;
        foreach (var id in ids)
        {
            try
            {
                await ProcessReviewAsync(id, cancellationToken);
                processedCount++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Anti-cheat ML review processing failed for DetectionId={DetectionId}. Continuing batch sweep.",
                    id);
            }
        }

        return processedCount;
    }

    public async Task<AntiCheatDetectionItemDto> ProcessReviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await db.AnswerPatternDetectionLogs
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Anti-cheat detection '{id}' was not found.");

        entity.MlReviewStatus = AntiCheatMlReviewStatuses.Processing;
        entity.MlReviewAttempts++;
        entity.MlLastAttemptAtUtc = DateTime.UtcNow;
        entity.MlLastError = null;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var review = BuildMlReview(entity);
            entity.MlReviewStatus = AntiCheatMlReviewStatuses.Completed;
            entity.MlReviewedAtUtc = DateTime.UtcNow;
            entity.MlModelName = review.ModelName;
            entity.MlReviewOutputJson = review.OutputJson;
            entity.MlLastError = null;

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            entity.MlReviewStatus = entity.MlReviewAttempts >= options.MlReviewMaxAttempts
                ? AntiCheatMlReviewStatuses.Failed
                : AntiCheatMlReviewStatuses.Queued;
            entity.MlLastError = ex.Message;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }

        return new AntiCheatDetectionItemDto(
            entity.Id,
            entity.UserId,
            entity.SourceType,
            entity.QuestionId,
            entity.TopicId,
            entity.SubtopicId,
            entity.SessionId,
            entity.DeviceId,
            entity.ClientSequence,
            entity.AnsweredAtUtc,
            entity.ResponseTimeMs,
            entity.IsCorrect,
            entity.Confidence,
            entity.RiskScore,
            entity.Severity,
            entity.Decision,
            entity.ReasonSummary,
            entity.ReviewStatus,
            entity.DetectedAtUtc,
            entity.ReviewedAtUtc,
            entity.SignalsJson,
            entity.PromptVersion,
            entity.MlReviewStatus,
            entity.MlReviewAttempts,
            entity.MlModelName,
            entity.MlReviewedAtUtc,
            entity.MlLastError,
            entity.MlReviewOutputJson);
    }

    private async Task<List<HistoricalAnswerSignal>> LoadHistoryAsync(
        string userId,
        DateTime lookbackStart,
        CancellationToken cancellationToken)
    {
        var userAnswersTask = db.UserAnswers
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.AnsweredAt >= lookbackStart)
            .OrderByDescending(x => x.AnsweredAt)
            .Take(options.MaxHistoryEvents)
            .Select(x => new
            {
                x.QuestionId,
                TopicId = (int?)null,
                SubtopicId = (int?)null,
                x.IsCorrect,
                ResponseTimeMs = x.TimeSpentSeconds * 1000,
                AnsweredAtUtc = x.AnsweredAt,
                x.Answer,
                Confidence = (double?)null,
                SourceType = "quiz"
            })
            .ToListAsync(cancellationToken);

        var adaptiveTask = db.UserQuestionHistories
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.AnsweredAt >= lookbackStart)
            .OrderByDescending(x => x.AnsweredAt)
            .Take(options.MaxHistoryEvents)
            .Select(x => new HistoricalAnswerSignal(
                x.QuestionId,
                x.TopicId,
                x.SubtopicId,
                x.IsCorrect,
                x.ResponseTimeSeconds * 1000,
                x.AnsweredAt,
                null,
                x.Confidence,
                "adaptive"))
            .ToListAsync(cancellationToken);

        var practiceTask = (
            from item in db.PracticeSessionItems.AsNoTracking()
            join session in db.PracticeSessions.AsNoTracking()
                on item.SessionId equals session.Id
            where session.UserId == userId &&
                  item.AnsweredAt != null &&
                  item.AnsweredAt >= lookbackStart &&
                  item.Correct != null
            orderby item.AnsweredAt descending
            select new HistoricalAnswerSignal(
                item.QuestionId,
                item.TopicId,
                item.SubtopicId,
                item.Correct!.Value,
                item.TimeSpentMs ?? 0,
                item.AnsweredAt!.Value,
                null,
                null,
                "practice"))
            .Take(options.MaxHistoryEvents)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(userAnswersTask, adaptiveTask, practiceTask);

        var history = userAnswersTask.Result
            .Select(x => new HistoricalAnswerSignal(
                x.QuestionId,
                x.TopicId,
                x.SubtopicId,
                x.IsCorrect,
                x.ResponseTimeMs,
                x.AnsweredAtUtc,
                Fingerprint(x.Answer),
                x.Confidence,
                x.SourceType))
            .Concat(adaptiveTask.Result)
            .Concat(practiceTask.Result)
            .OrderBy(x => x.AnsweredAtUtc)
            .TakeLast(options.MaxHistoryEvents)
            .ToList();

        return history;
    }

    private IReadOnlyDictionary<string, object?> BuildFeatureSnapshot(
        IReadOnlyList<HistoricalAnswerSignal> history,
        AntiCheatAnswerObservationInput input)
    {
        var currentFingerprint = Fingerprint(input.Answer);
        var current = new HistoricalAnswerSignal(
            input.QuestionId,
            input.TopicId,
            input.SubtopicId,
            input.IsCorrect,
            Math.Max(0, input.ResponseTimeMs),
            input.AnsweredAtUtc,
            currentFingerprint,
            input.Confidence,
            input.SourceType);

        var sample = history
            .TakeLast(options.MaxHistoryEvents - 1)
            .Append(current)
            .ToList();

        var rapidWindowStart = input.AnsweredAtUtc.AddMinutes(-5);
        var rapidWindow = sample.Where(x => x.AnsweredAtUtc >= rapidWindowStart).ToList();
        var fastCorrectCount = rapidWindow.Count(x => x.IsCorrect && x.ResponseTimeMs > 0 && x.ResponseTimeMs <= options.FastResponseThresholdMs);
        var accuracy = sample.Count == 0 ? 0d : sample.Count(x => x.IsCorrect) / (double)sample.Count;
        var avgResponseMs = sample.Count == 0 ? 0d : sample.Average(x => x.ResponseTimeMs);
        var responseStdDevMs = sample.Count < 2 ? 0d : StdDev(sample.Select(x => (double)x.ResponseTimeMs));
        var sameFingerprintCount = string.IsNullOrWhiteSpace(currentFingerprint)
            ? 0
            : rapidWindow.Count(x => x.AnswerFingerprint == currentFingerprint);
        var cadenceStdDevMs = sample.Count < 2
            ? 0d
            : StdDev(sample.Zip(sample.Skip(1), (left, right) => (double)(right.AnsweredAtUtc - left.AnsweredAtUtc).TotalMilliseconds));
        var consecutiveCorrect = CountTrailing(sample, x => x.IsCorrect);
        var identicalResponseTimeCount = sample
            .GroupBy(x => x.ResponseTimeMs)
            .Select(x => x.Count())
            .DefaultIfEmpty(0)
            .Max();
        var mixedSourceCount = sample.Select(x => x.SourceType).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        return new Dictionary<string, object?>
        {
            ["sampleSize"] = sample.Count,
            ["fastCorrectCount5m"] = fastCorrectCount,
            ["accuracySample"] = Math.Round(accuracy, 4),
            ["avgResponseMs"] = Math.Round(avgResponseMs, 2),
            ["responseStdDevMs"] = Math.Round(responseStdDevMs, 2),
            ["sameAnswerFingerprintCount5m"] = sameFingerprintCount,
            ["cadenceStdDevMs"] = Math.Round(cadenceStdDevMs, 2),
            ["consecutiveCorrect"] = consecutiveCorrect,
            ["identicalResponseTimeCount"] = identicalResponseTimeCount,
            ["mixedSourceCount"] = mixedSourceCount,
            ["currentResponseMs"] = current.ResponseTimeMs,
            ["currentIsCorrect"] = current.IsCorrect,
            ["currentConfidence"] = current.Confidence,
            ["currentFingerprintPresent"] = !string.IsNullOrWhiteSpace(currentFingerprint)
        };
    }

    private AntiCheatDetectionResultDto BuildDetectionResult(
        AntiCheatAnswerObservationInput input,
        IReadOnlyDictionary<string, object?> featureSnapshot)
    {
        var sampleSize = Convert.ToInt32(featureSnapshot["sampleSize"] ?? 0);
        var fastCorrectCount = Convert.ToInt32(featureSnapshot["fastCorrectCount5m"] ?? 0);
        var accuracy = Convert.ToDouble(featureSnapshot["accuracySample"] ?? 0d);
        var avgResponseMs = Convert.ToDouble(featureSnapshot["avgResponseMs"] ?? 0d);
        var responseStdDevMs = Convert.ToDouble(featureSnapshot["responseStdDevMs"] ?? 0d);
        var sameFingerprintCount = Convert.ToInt32(featureSnapshot["sameAnswerFingerprintCount5m"] ?? 0);
        var cadenceStdDevMs = Convert.ToDouble(featureSnapshot["cadenceStdDevMs"] ?? 0d);
        var consecutiveCorrect = Convert.ToInt32(featureSnapshot["consecutiveCorrect"] ?? 0);
        var identicalResponseTimeCount = Convert.ToInt32(featureSnapshot["identicalResponseTimeCount"] ?? 0);

        var signals = new List<string>(5);
        var riskScore = 0;

        if (fastCorrectCount >= options.RapidCorrectBurstThreshold)
        {
            riskScore += 35;
            signals.Add($"rapid_correct_burst:{fastCorrectCount}/5m");
        }

        if (sampleSize >= options.LowVarianceSampleSize &&
            avgResponseMs > 0 &&
            avgResponseMs <= options.MaxAverageResponseMs &&
            responseStdDevMs <= options.MaxResponseStdDevMs)
        {
            riskScore += 25;
            signals.Add($"low_variance_timing:stddev={Math.Round(responseStdDevMs, 2)}");
        }

        if (sampleSize >= options.MinAccuracySampleSize &&
            accuracy >= options.MinHighAccuracy &&
            avgResponseMs > 0 &&
            avgResponseMs <= options.MaxAverageResponseMs)
        {
            riskScore += 25;
            signals.Add($"improbable_accuracy_speed:accuracy={Math.Round(accuracy, 2)},avgMs={Math.Round(avgResponseMs, 0)}");
        }

        if (ShouldUseFingerprintRule(input.Answer) &&
            sameFingerprintCount >= options.RepeatedAnswerThreshold)
        {
            riskScore += 20;
            signals.Add($"repeated_answer_pattern:{sameFingerprintCount}/5m");
        }

        if (sampleSize >= options.RegularCadenceSampleSize &&
            cadenceStdDevMs <= options.RegularCadenceStdDevMs)
        {
            riskScore += 20;
            signals.Add($"regular_cadence:stddev={Math.Round(cadenceStdDevMs, 2)}");
        }

        if (input.IsCorrect &&
            input.Confidence is >= 0.95d &&
            input.ResponseTimeMs > 0 &&
            input.ResponseTimeMs <= options.FastResponseThresholdMs / 2)
        {
            riskScore += 10;
            signals.Add("high_confidence_ultrafast_correct");
        }

        if (identicalResponseTimeCount >= Math.Max(4, options.LowVarianceSampleSize / 2))
        {
            riskScore += 10;
            signals.Add($"repeated_exact_timing:{identicalResponseTimeCount}");
        }

        if (consecutiveCorrect >= Math.Max(8, options.RapidCorrectBurstThreshold))
        {
            riskScore += 10;
            signals.Add($"long_perfect_streak:{consecutiveCorrect}");
        }

        var isSuspicious = riskScore >= options.DetectionThreshold;
        var severity = riskScore >= 80
            ? AntiCheatDetectionSeverities.Critical
            : riskScore >= 65
                ? AntiCheatDetectionSeverities.High
                : AntiCheatDetectionSeverities.Medium;
        var decision = riskScore >= 80
            ? AntiCheatDetectionDecisions.LikelyBot
            : AntiCheatDetectionDecisions.Review;
        var reasonSummary = signals.Count == 0
            ? "No suspicious answer-pattern signals were detected."
            : $"Suspicious answer behavior detected: {string.Join("; ", signals)}.";

        return new AntiCheatDetectionResultDto(
            isSuspicious,
            riskScore,
            severity,
            decision,
            reasonSummary,
            signals,
            new AntiCheatMlPromptDto(options.PromptVersion, string.Empty, string.Empty, "{}"));
    }

    private static bool ShouldUseFingerprintRule(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        var trimmed = answer.Trim();
        if (trimmed.Length <= 1)
        {
            return false;
        }

        return !trimmed.All(char.IsDigit);
    }

    private static int CountTrailing<T>(IReadOnlyList<T> source, Func<T, bool> predicate)
    {
        var count = 0;
        for (var i = source.Count - 1; i >= 0; i--)
        {
            if (!predicate(source[i]))
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static double StdDev(IEnumerable<double> values)
    {
        var sample = values.ToArray();
        if (sample.Length < 2)
        {
            return 0d;
        }

        var avg = sample.Average();
        var variance = sample.Sum(x => Math.Pow(x - avg, 2)) / sample.Length;
        return Math.Sqrt(variance);
    }

    private static string? Fingerprint(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return null;
        }

        var normalized = answer.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash[..8]);
    }

    private AntiCheatMlReviewResultDto BuildMlReview(AnswerPatternDetectionLog entity)
    {
        var signals = JsonSerializer.Deserialize<List<string>>(entity.SignalsJson) ?? [];
        var classification = entity.RiskScore >= 85
            ? "likely_bot"
            : entity.RiskScore >= options.DetectionThreshold
                ? "needs_review"
                : "likely_human";
        var recommendedAction = entity.RiskScore >= 85
            ? "temporary_hold"
            : entity.RiskScore >= 65
                ? "rate_limit"
                : "review";
        var confidence = Math.Round(Math.Clamp(entity.RiskScore / 100d, 0.10d, 0.99d), 2);
        var summary = $"ML-review fallback classified event as '{classification}' based on signals: {string.Join(", ", signals)}";

        var outputJson = JsonSerializer.Serialize(new
        {
            classification,
            confidence,
            reasons = signals,
            recommendedAction
        });

        return new AntiCheatMlReviewResultDto(
            classification,
            confidence,
            recommendedAction,
            summary,
            options.MlReviewModelName,
            outputJson);
    }
}

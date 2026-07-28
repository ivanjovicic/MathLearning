using MathLearning.Application.DTOs.Quiz;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Domain.Events;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Idempotency;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.EventBus.Handlers;

public sealed class AdaptiveAnswerLegacySrsSyncRequestedHandler : IEventHandler<AdaptiveAnswerLegacySrsSyncRequested>
{
    private const string Endpoint = "POST /internal/adaptive/legacy-srs-sync";

    private readonly ApiDbContext db;
    private readonly ISrsService srsService;
    private readonly IIdempotencyLedgerService idempotencyService;
    private readonly ILogger<AdaptiveAnswerLegacySrsSyncRequestedHandler> logger;

    public AdaptiveAnswerLegacySrsSyncRequestedHandler(
        ApiDbContext db,
        ISrsService srsService,
        IIdempotencyLedgerService idempotencyService,
        ILogger<AdaptiveAnswerLegacySrsSyncRequestedHandler> logger)
    {
        this.db = db;
        this.srsService = srsService;
        this.idempotencyService = idempotencyService;
        this.logger = logger;
    }

    public async Task Handle(AdaptiveAnswerLegacySrsSyncRequested ev, CancellationToken ct)
    {
        var requestPayload = new
        {
            questionId = ev.QuestionId,
            isCorrect = ev.IsCorrect,
            timeMs = Math.Max(0, ev.ResponseTimeSeconds) * 1000
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var begin = await idempotencyService.BeginOrGetExistingAsync(
                ev.UserId,
                QuizOperationTypes.SrsUpdate,
                ev.Id.ToString("N"),
                ev.Id.ToString("N"),
                Endpoint,
                requestPayload,
                ct);

            if (begin.IsCompleted || begin.IsFailed)
            {
                logger.LogInformation(
                    "Adaptive legacy SRS sync replay skipped. UserId={UserId} QuestionId={QuestionId} SessionId={SessionId} SessionItemId={SessionItemId}",
                    ev.UserId,
                    ev.QuestionId,
                    ev.AdaptiveSessionId,
                    ev.AdaptiveSessionItemId);

                await transaction.CommitAsync(ct);
                return;
            }

            if (begin.IsPending)
            {
                logger.LogInformation(
                    "Recovering pending adaptive legacy SRS sync. UserId={UserId} QuestionId={QuestionId} SessionId={SessionId} SessionItemId={SessionItemId}",
                    ev.UserId,
                    ev.QuestionId,
                    ev.AdaptiveSessionId,
                    ev.AdaptiveSessionItemId);
            }

            var stat = await srsService.UpdateAsync(
                ev.UserId,
                new SrsUpdateDto
                {
                    QuestionId = ev.QuestionId,
                    IsCorrect = ev.IsCorrect,
                    TimeMs = Math.Max(0, ev.ResponseTimeSeconds) * 1000
                },
                ct);

            var responseBody = new
            {
                questionId = stat.QuestionId,
                nextReview = stat.NextReview,
                streak = stat.SuccessStreak,
                ease = stat.Ease
            };

            await idempotencyService.CompleteAsync(
                begin.LedgerId,
                responseBody,
                StatusCodes.Status200OK,
                ct);

            await transaction.CommitAsync(ct);

            logger.LogInformation(
                "Adaptive legacy SRS sync processed. UserId={UserId} QuestionId={QuestionId} SessionId={SessionId} SessionItemId={SessionItemId}",
                ev.UserId,
                ev.QuestionId,
                ev.AdaptiveSessionId,
                ev.AdaptiveSessionItemId);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    private static async Task RollbackQuietlyAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch
        {
        }
    }
}

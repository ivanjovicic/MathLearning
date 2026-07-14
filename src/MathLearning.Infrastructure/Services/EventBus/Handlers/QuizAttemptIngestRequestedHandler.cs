using MathLearning.Application.Services;
using MathLearning.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.EventBus.Handlers;

public sealed class QuizAttemptIngestRequestedHandler : IEventHandler<QuizAttemptIngestRequested>
{
    private readonly IQuizAttemptIngestService service;
    private readonly ILogger<QuizAttemptIngestRequestedHandler> logger;

    public QuizAttemptIngestRequestedHandler(
        IQuizAttemptIngestService service,
        ILogger<QuizAttemptIngestRequestedHandler> logger)
    {
        this.service = service;
        this.logger = logger;
    }

    public async Task Handle(QuizAttemptIngestRequested ev, CancellationToken ct)
    {
        await service.IngestAttemptsAsync(
            ev.UserId,
            [new QuizAttemptIngestItem(
                ev.AttemptKey,
                ev.QuizId,
                ev.QuestionId,
                ev.SubtopicId,
                ev.Correct,
                ev.TimeSpentMs,
                ev.CreatedAtUtc)],
            ct);

        logger.LogInformation(
            "Quiz attempt ingest event handled. UserId={UserId} AttemptKeySuffix={AttemptKeySuffix} QuestionId={QuestionId}",
            ev.UserId,
            ev.AttemptKey.Length <= 12 ? ev.AttemptKey : ev.AttemptKey[^12..],
            ev.QuestionId);
    }
}

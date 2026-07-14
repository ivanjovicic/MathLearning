using System.Text.Json;
using MathLearning.Domain.Events;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Persistance.Models;

namespace MathLearning.Api.Services;

internal static class QuizAttemptIngestOutbox
{
    public static void Enqueue(ApiDbContext db, QuizAttemptIngestRequested ev)
    {
        db.Outbox.Add(new OutboxMessage
        {
            Id = ev.Id,
            OccurredUtc = ev.OccurredUtc,
            Type = ev.GetType().AssemblyQualifiedName!,
            PayloadJson = JsonSerializer.Serialize(ev, ev.GetType())
        });
    }
}

using MathLearning.Domain.Events;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.EventBus.Handlers;

public class QuizCompletedCoinsHandler : IEventHandler<QuizCompleted>
{
    private readonly AppDbContext _db;
    private readonly ILogger<QuizCompletedCoinsHandler> _logger;

    public QuizCompletedCoinsHandler(AppDbContext db, ILogger<QuizCompletedCoinsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(QuizCompleted ev, CancellationToken ct)
    {
        _logger.LogInformation("🪙 QuizCompleted: User {UserId} answered {Correct}/{Total} correctly, awarding coins", 
            ev.UserId, ev.Correct, ev.Total);

        // Npr. 1 coin po tačnom odgovoru
        var progress = await _db.UserProgress.SingleOrDefaultAsync(x => x.UserId == ev.UserId, ct);
        
        if (progress != null)
        {
            progress.GrantCoins(ev.Correct, "quiz_completed");
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("✅ Granted {Coins} coins to user {UserId}", ev.Correct, ev.UserId);
        }
        else
        {
            _logger.LogWarning("⚠️ UserProgress not found for user {UserId}", ev.UserId);
        }
    }
}

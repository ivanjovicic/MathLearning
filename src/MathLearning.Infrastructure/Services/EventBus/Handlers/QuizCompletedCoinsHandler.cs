using MathLearning.Domain.Events;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.EventBus.Handlers;

public class QuizCompletedCoinsHandler : IEventHandler<QuizCompleted>
{
    private readonly ApiDbContext _db;
    private readonly ILogger<QuizCompletedCoinsHandler> _logger;

    public QuizCompletedCoinsHandler(ApiDbContext db, ILogger<QuizCompletedCoinsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(QuizCompleted ev, CancellationToken ct)
    {
        _logger.LogInformation(
            "QuizCompleted: User {UserId} answered {Correct}/{Total} correctly, awarding coins",
            ev.UserId, ev.Correct, ev.Total);

        // 1 coin per correct answer. Coins live on UserProfile in ApiDbContext.
        var profile = await _db.UserProfiles.SingleOrDefaultAsync(x => x.UserId == ev.UserId, ct);
        if (profile is null)
        {
            _logger.LogWarning("UserProfile not found for user {UserId}; cannot award coins", ev.UserId);
            return;
        }

        var amount = Math.Max(0, ev.Correct);
        if (amount == 0)
        {
            return;
        }

        profile.Coins += amount;
        profile.TotalCoinsEarned += amount;
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Granted {Coins} coins to user {UserId}", amount, ev.UserId);
    }
}


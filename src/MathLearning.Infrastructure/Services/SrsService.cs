using MathLearning.Application.DTOs.Quiz;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services;

public class SrsService : ISrsService
{
    private readonly ApiDbContext _db;
    private readonly double[] _baseIntervals = new[] { 1d, 2d, 4d, 7d, 15d };

    public SrsService(ApiDbContext db)
    {
        _db = db;
    }

    public async Task<QuestionStat> UpdateAsync(int userId, SrsUpdateDto dto)
    {
        var stat = await _db.QuestionStats
            .FirstOrDefaultAsync(x => x.UserId == userId && x.QuestionId == dto.QuestionId);

        if (stat == null)
        {
            stat = new QuestionStat
            {
                UserId = userId,
                QuestionId = dto.QuestionId
            };
            _db.QuestionStats.Add(stat);
        }

        if (dto.IsCorrect)
        {
            stat.SuccessStreak++;
            stat.Ease = Math.Min(3.0, stat.Ease + 0.05);
        }
        else
        {
            stat.SuccessStreak = 0;
            stat.Ease = Math.Max(1.0, stat.Ease - 0.1);
        }

        var index = Math.Min(stat.SuccessStreak, _baseIntervals.Length - 1);
        var intervalDays = _baseIntervals[index] * stat.Ease;

        stat.NextReview = DateTime.UtcNow.AddDays(intervalDays);
        stat.LastAnswered = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await UpdateDailyStreakAsync(userId);

        return stat;
    }

    private async Task UpdateDailyStreakAsync(int userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var entry = await _db.UserDailyStats
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Day == today);

        if (entry == null)
        {
            entry = new UserDailyStat
            {
                UserId = userId,
                Day = today,
                Completed = false
            };
            _db.UserDailyStats.Add(entry);
        }

        var solvedToday = await _db.QuestionStats
            .Where(x => x.UserId == userId && x.LastAnswered >= DateTime.UtcNow.Date)
            .CountAsync();

        if (solvedToday >= 5 && !entry.Completed)
        {
            entry.Completed = true;

            var profile = await _db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile != null)
            {
                var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
                var didYesterday = await _db.UserDailyStats
                    .AnyAsync(x => x.UserId == userId && x.Day == yesterday && x.Completed);

                if (!didYesterday)
                    profile.Streak = 0;

                profile.Streak++;
                profile.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
    }
}

using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Data;

namespace MathLearning.Tests.Services;

public class XpTrackingConcurrencyIntegrationTests
{
    [Fact]
    public async Task ConcurrentFirstCorrectSubmissions_AwardXpOnlyOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new ApiDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            await SeedAsync(setup);
        }

        var t1 = SubmitCorrectAttemptAsync(options, "c1");
        var t2 = SubmitCorrectAttemptAsync(options, "c2");
        await Task.WhenAll(t1, t2);

        await using var verify = new ApiDbContext(options);
        var profile = await verify.UserProfiles.FirstAsync(p => p.UserId == "1");
        var awardedAuditCount = await verify.UserAnswerAudits.CountAsync(a => a.AwardedXp > 0);

        Assert.Equal(10, profile.Xp);
        Assert.Equal(1, awardedAuditCount);
    }

    private static async Task SubmitCorrectAttemptAsync(DbContextOptions<ApiDbContext> options, string clientId)
    {
        await using var db = new ApiDbContext(options);
        var service = new XpTrackingService(
            db,
            Options.Create(new XpTrackingOptions
            {
                EnableXpCaps = true,
                DailyXpCap = 500,
                WeeklyXpCap = 2000,
                MonthlyXpCap = 6000
            }),
            NullLogger<XpTrackingService>.Instance);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var stat = await db.UserQuestionStats.FirstOrDefaultAsync(s => s.UserId == "1" && s.QuestionId == 1);
            if (stat == null)
            {
                stat = new UserQuestionStat
                {
                    UserId = "1",
                    QuestionId = 1
                };
                db.UserQuestionStats.Add(stat);
            }

            var isFirstTimeCorrect = stat.CorrectAttempts == 0;
            stat.Attempts++;
            stat.CorrectAttempts++;
            stat.LastAttemptAt = DateTime.UtcNow;

            var award = isFirstTimeCorrect
                ? await service.AddXpWithinTransactionAsync("1", 10, false, "integration_test", db)
                : new XpAwardResult(0, (await db.UserProfiles.FirstAsync(p => p.UserId == "1")).Xp, "already_awarded", 0);

            db.UserAnswerAudits.Add(new UserAnswerAudit
            {
                UserId = "1",
                QuestionId = 1,
                Source = "integration_test",
                IsOffline = false,
                ClientId = clientId,
                Answer = "2",
                IsCorrect = true,
                IsFirstTimeCorrect = isFirstTimeCorrect,
                AwardedXp = award.AwardedXp,
                TotalXpAfterAward = award.TotalXpAfterAward,
                Reason = award.Reason,
                AnsweredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
        }
    }

    private static async Task SeedAsync(ApiDbContext db)
    {
        db.Users.Add(new IdentityUser
        {
            Id = "1",
            UserName = "testuser"
        });

        db.UserProfiles.Add(new UserProfile
        {
            UserId = "1",
            Username = "testuser"
        });

        var category = new Category("Algebra");
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var topic = new Topic("Topic", "desc");
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        var subtopic = new Subtopic("Subtopic", topic.Id);
        db.Subtopics.Add(subtopic);
        await db.SaveChangesAsync();

        var question = new Question("1+1=?", 1, category.Id);
        question.SetSubtopic(subtopic.Id);
        question.ReplaceOptions([
            new QuestionOption("2", true),
            new QuestionOption("3", false)
        ]);
        db.Questions.Add(question);
        await db.SaveChangesAsync();
    }
}

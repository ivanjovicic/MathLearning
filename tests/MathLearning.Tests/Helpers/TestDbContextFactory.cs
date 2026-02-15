using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ApiDbContext Create(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new ApiDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public static async Task<ApiDbContext> CreateWithSeedAsync(string? dbName = null)
    {
        var db = Create(dbName);
        await SeedAsync(db);
        return db;
    }

    public static async Task SeedAsync(ApiDbContext db)
    {
        var category = new Category("Algebra");
        db.Categories.Add(category);

        var topic = new Topic("Osnove Algebre", "Osnovni pojmovi");
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        var subtopic = new Subtopic("Jednačine", topic.Id);
        db.Subtopics.Add(subtopic);
        await db.SaveChangesAsync();

        for (int i = 1; i <= 20; i++)
        {
            var q = new Question($"Koliko je {i} + {i}?", (i % 3) + 1, category.Id);
            q.SetSubtopic(subtopic.Id);

            var options = new List<QuestionOption>
            {
                new($"{i * 2}", true),
                new($"{i * 2 + 1}", false),
                new($"{i * 2 - 1}", false),
                new($"{i * 3}", false),
            };
            q.ReplaceOptions(options);

            db.Questions.Add(q);
        }

        // Identity principal (required by 1:1 FK from UserProfile)
        db.Users.Add(new IdentityUser
        {
            Id = "1",
            UserName = "testuser",
            Email = "testuser@example.com"
        });

        var profile = new UserProfile
        {
            UserId = "1",
            Username = "testuser",
            DisplayName = "Test User",
            Coins = 100,
            Level = 1,
            Xp = 0,
            Streak = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.UserProfiles.Add(profile);

        await db.SaveChangesAsync();
    }
}

using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MathLearning.Api.Startup;

public sealed class TestAccountSeeder
{
    private readonly ILogger<TestAccountSeeder> logger;
    private readonly UserManager<IdentityUser> userManager;
    private readonly ApiDbContext db;

    private static readonly SeedAccount[] Accounts =
    [
        new("test", "test@mathlearning.local", "test-passphrase-2026!"),
        new("student", "student@mathlearning.local", "student-passphrase-2026!")
    ];

    public TestAccountSeeder(
        ILogger<TestAccountSeeder> logger,
        UserManager<IdentityUser> userManager,
        ApiDbContext db)
    {
        this.logger = logger;
        this.userManager = userManager;
        this.db = db;
    }

    public async Task SeedAsync(IHostEnvironment environment, CancellationToken ct = default)
    {
        var shouldSeed = environment.IsDevelopment() || environment.IsEnvironment("Test");
        if (!shouldSeed)
        {
            logger.LogInformation(
                "Skipping test account seeding in environment {Environment}. Development/Test only.",
                environment.EnvironmentName);
            return;
        }

        foreach (var account in Accounts)
        {
            await SeedAccountAsync(account, ct);
        }
    }

    private async Task SeedAccountAsync(SeedAccount account, CancellationToken ct)
    {
        var user = await userManager.FindByNameAsync(account.Username);
        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = account.Username,
                Email = account.Email,
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var createResult = await userManager.CreateAsync(user, account.Password);
            if (!createResult.Succeeded)
            {
                logger.LogWarning(
                    "Failed to create seeded test account {Username}: {Errors}",
                    account.Username,
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Created seeded test account {Username}.", account.Username);
        }
        else
        {
            logger.LogInformation("Seeded test account {Username} already exists.", account.Username);
        }

        await EnsureUserProfileAsync(user, account, ct);
    }

    private async Task EnsureUserProfileAsync(IdentityUser user, SeedAccount account, CancellationToken ct)
    {
        var existingProfile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        if (existingProfile is not null)
        {
            logger.LogInformation("Seeded test account profile already exists for {Username}.", account.Username);
            return;
        }

        var now = DateTime.UtcNow;
        var profile = new UserProfile
        {
            UserId = user.Id,
            Username = account.Username,
            DisplayName = account.Username,
            Coins = 100,
            Level = 1,
            Xp = 0,
            Streak = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created seeded test account profile for {Username}.", account.Username);
    }

    private sealed record SeedAccount(string Username, string Email, string Password);
}

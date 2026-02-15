# 🎯 XP Tracking Integration - Quick Start

## Adding XP When Users Complete Activities

### Step 1: Inject XpTrackingService

In your endpoint or service where users earn XP:

```csharp
using MathLearning.Infrastructure.Services;

public class QuizEndpoints
{
    private readonly ApiDbContext _db;
    private readonly XpTrackingService _xpService;

    public static void MapQuizEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quiz")
            .RequireAuthorization()
            .WithTags("Quiz");

        group.MapPost("/submit", async (
            ApiDbContext db,
            XpTrackingService xpService,  // ← Inject here
            HttpContext ctx,
            SubmitAnswerRequest request) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            
            // Your existing quiz logic...
            bool isCorrect = CheckAnswer(request);
            
            if (isCorrect)
            {
                // Award XP for correct answer
                int xpEarned = CalculateXp(request.Difficulty);
                await xpService.AddXpAsync(userId, xpEarned);
                
                // XpTrackingService automatically updates:
                // - Xp (total)
                // - DailyXp
                // - WeeklyXp
                // - MonthlyXp
                // - Level
            }
            
            return Results.Ok(new { isCorrect, xpEarned });
        });
    }
}
```

### Step 2: Calculate XP Based on Activity

```csharp
public static int CalculateXp(int difficulty, bool usedHints = false)
{
    int baseXp = difficulty switch
    {
        1 => 10,  // Easy
        2 => 20,  // Medium
        3 => 30,  // Hard
        _ => 10
    };
    
    // Reduce XP if hints were used
    if (usedHints)
    {
        baseXp = (int)(baseXp * 0.7);
    }
    
    return baseXp;
}
```

### Step 3: Return XP in Response

```csharp
return Results.Ok(new 
{
    isCorrect = true,
    xpEarned = xpAmount,
    newTotalXp = profile.Xp,
    newLevel = profile.Level,
    dailyXp = profile.DailyXp,
    weeklyXp = profile.WeeklyXp
});
```

## Example: Complete Quiz Flow

```csharp
group.MapPost("/complete", async (
    ApiDbContext db,
    XpTrackingService xpService,
    HttpContext ctx,
    CompleteQuizRequest request) =>
{
    string userId = ctx.User.FindFirst("userId")!.Value;
    
    // Calculate results
    int correctAnswers = request.Answers.Count(a => a.IsCorrect);
    int totalQuestions = request.Answers.Count;
    double percentage = (double)correctAnswers / totalQuestions;
    
    // Calculate XP based on performance
    int baseXp = 100; // Base XP for completing quiz
    int bonusXp = (int)(percentage * 50); // Up to 50 bonus XP
    int totalXp = baseXp + bonusXp;
    
    // Award XP
    var profile = await xpService.AddXpAsync(userId, totalXp);
    
    return Results.Ok(new
    {
        correctAnswers,
        totalQuestions,
        percentage,
        xpEarned = totalXp,
        newTotalXp = profile.Xp,
        newLevel = profile.Level,
        leveledUp = (profile.Xp - totalXp) / 100 < profile.Xp / 100
    });
});
```

## Displaying User's Leaderboard Position

```csharp
group.MapGet("/my-stats", async (
    ApiDbContext db,
    LeaderboardService leaderboardService,
    HttpContext ctx) =>
{
    string userId = ctx.User.FindFirst("userId")!.Value;
    
    var profile = await db.UserProfiles.FindAsync(userId);
    
    // Get weekly leaderboard position
    var weeklyLeaderboard = await leaderboardService
        .GetLeaderboardAsync(userId, "global", "week", 10);
    
    return Results.Ok(new
    {
        profile = new
        {
            profile.Xp,
            profile.Level,
            profile.Streak,
            profile.DailyXp,
            profile.WeeklyXp,
            profile.MonthlyXp
        },
        weeklyRank = weeklyLeaderboard.Me?.Rank,
        topPlayers = weeklyLeaderboard.Items.Take(5)
    });
});
```

## Admin Endpoint: Reset User XP (Optional)

```csharp
group.MapPost("/admin/reset-xp/{userId}", async (
    XpTrackingService xpService,
    string userId) =>
{
    await xpService.ResetTimeBasedXpAsync(userId);
    return Results.Ok("XP reset successful");
})
.RequireAuthorization("Admin");
```

## Testing XP System

```csharp
[Fact]
public async Task AddXp_UpdatesAllTimePeriods()
{
    // Arrange
    var options = new DbContextOptionsBuilder<ApiDbContext>()
        .UseInMemoryDatabase("TestDb")
        .Options;
    
    using var db = new ApiDbContext(options);
    var xpService = new XpTrackingService(db);
    
    var profile = new UserProfile
    {
        UserId = "test-user",
        Username = "testuser"
    };
    db.UserProfiles.Add(profile);
    await db.SaveChangesAsync();
    
    // Act
    await xpService.AddXpAsync("test-user", 50);
    
    // Assert
    var updated = await db.UserProfiles.FindAsync("test-user");
    Assert.Equal(50, updated.Xp);
    Assert.Equal(50, updated.DailyXp);
    Assert.Equal(50, updated.WeeklyXp);
    Assert.Equal(50, updated.MonthlyXp);
    Assert.Equal(1, updated.Level);
}
```

## Common Patterns

### 1. Award Bonus XP for Streaks

```csharp
if (profile.Streak >= 7)
{
    int bonusXp = 50; // Bonus for 7-day streak
    await xpService.AddXpAsync(userId, bonusXp);
}
```

### 2. Daily XP Cap (Anti-Farming)

```csharp
const int DAILY_XP_CAP = 1000;

var profile = await db.UserProfiles.FindAsync(userId);
if (profile.DailyXp >= DAILY_XP_CAP)
{
    return Results.BadRequest("Daily XP limit reached");
}

await xpService.AddXpAsync(userId, xpAmount);
```

### 3. Multiplier Events

```csharp
public async Task<int> AwardXpWithMultiplier(string userId, int baseXp)
{
    // Check for active events
    bool isWeekendBonus = DateTime.UtcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    double multiplier = isWeekendBonus ? 1.5 : 1.0;
    
    int finalXp = (int)(baseXp * multiplier);
    await _xpService.AddXpAsync(userId, finalXp);
    
    return finalXp;
}
```

## Integration Checklist

- [ ] Inject `XpTrackingService` in endpoints where XP is awarded
- [ ] Call `AddXpAsync()` after successful activities
- [ ] Return XP information in API responses
- [ ] Test XP tracking across different activities
- [ ] Verify daily/weekly/monthly resets work correctly
- [ ] Add XP information to user profile endpoints
- [ ] Update mobile/web clients to display XP and leaderboard data
- [ ] Create admin tools for XP management (optional)
- [ ] Monitor performance of leaderboard queries
- [ ] Set up alerts for background service failures

## Quick Reference

| Activity | Recommended XP |
|----------|---------------|
| Correct answer (easy) | 10 XP |
| Correct answer (medium) | 20 XP |
| Correct answer (hard) | 30 XP |
| Complete quiz (10 questions) | 100 XP |
| Daily streak bonus | 50 XP |
| Weekly goal completion | 200 XP |
| First daily activity | 25 XP |

Adjust XP values based on your game economy and user engagement goals!

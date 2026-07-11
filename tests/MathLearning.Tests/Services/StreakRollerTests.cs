using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Services;

namespace MathLearning.Tests.Services;

public sealed class StreakRollerTests
{
    private static readonly DateOnly Today = new(2026, 7, 11);

    [Fact]
    public void Apply_WithNoActiveStreak_ReturnsNullWithoutMutation()
    {
        var profile = CreateProfile(streak: 0, lastStreakDay: Today.AddDays(-5), freezes: 3);
        var updatedAt = profile.UpdatedAt;

        var result = StreakRoller.Apply(profile, Today);

        Assert.Null(result);
        Assert.Equal(0, profile.Streak);
        Assert.Equal(3, profile.StreakFreezeCount);
        Assert.Equal(Today.AddDays(-5), profile.LastStreakDay);
        Assert.Equal(updatedAt, profile.UpdatedAt);
    }

    [Fact]
    public void Apply_WithMissingLastStreakDay_ReturnsNullWithoutMutation()
    {
        var profile = CreateProfile(streak: 8, lastStreakDay: null, freezes: 2);
        var updatedAt = profile.UpdatedAt;

        var result = StreakRoller.Apply(profile, Today);

        Assert.Null(result);
        Assert.Equal(8, profile.Streak);
        Assert.Equal(2, profile.StreakFreezeCount);
        Assert.Null(profile.LastStreakDay);
        Assert.Equal(updatedAt, profile.UpdatedAt);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void Apply_WhenNoDayWasMissed_ReturnsNull(int lastDayOffset)
    {
        var lastDay = Today.AddDays(lastDayOffset);
        var profile = CreateProfile(streak: 4, lastStreakDay: lastDay, freezes: 1);

        var result = StreakRoller.Apply(profile, Today);

        Assert.Null(result);
        Assert.Equal(4, profile.Streak);
        Assert.Equal(1, profile.StreakFreezeCount);
        Assert.Equal(lastDay, profile.LastStreakDay);
    }

    [Fact]
    public void Apply_WithEnoughFreezes_PreservesStreakAndConsumesMissedDays()
    {
        var profile = CreateProfile(streak: 7, lastStreakDay: Today.AddDays(-3), freezes: 3);

        var result = StreakRoller.Apply(profile, Today);

        Assert.NotNull(result);
        Assert.Equal("streak_freeze_used", result!.Type);
        Assert.Equal(2, result.MissedDays);
        Assert.Equal(2, result.FreezesUsed);
        Assert.Equal(7, result.StreakBefore);
        Assert.Equal(7, result.StreakAfter);
        Assert.Equal(7, profile.Streak);
        Assert.Equal(1, profile.StreakFreezeCount);
        Assert.Equal(Today.AddDays(-1), profile.LastStreakDay);
        Assert.True(profile.UpdatedAt > DateTime.UnixEpoch);
    }

    [Fact]
    public void Apply_WithMoreFreezesThanNeeded_ConsumesOnlyRequiredFreeze()
    {
        var profile = CreateProfile(streak: 12, lastStreakDay: Today.AddDays(-2), freezes: 5);

        var result = StreakRoller.Apply(profile, Today);

        Assert.NotNull(result);
        Assert.Equal("streak_freeze_used", result!.Type);
        Assert.Equal(1, result.MissedDays);
        Assert.Equal(1, result.FreezesUsed);
        Assert.Equal(4, profile.StreakFreezeCount);
        Assert.Equal(12, profile.Streak);
        Assert.Equal(Today.AddDays(-1), profile.LastStreakDay);
    }

    [Fact]
    public void Apply_WithInsufficientFreezes_ResetsStreakAndReportsUnprotectedDays()
    {
        var profile = CreateProfile(streak: 9, lastStreakDay: Today.AddDays(-4), freezes: 1);

        var result = StreakRoller.Apply(profile, Today);

        Assert.NotNull(result);
        Assert.Equal("streak_reset", result!.Type);
        Assert.Equal(2, result.MissedDays);
        Assert.Equal(1, result.FreezesUsed);
        Assert.Equal(9, result.StreakBefore);
        Assert.Equal(0, result.StreakAfter);
        Assert.Equal(0, profile.Streak);
        Assert.Equal(0, profile.StreakFreezeCount);
        Assert.Null(profile.LastStreakDay);
        Assert.True(profile.UpdatedAt > DateTime.UnixEpoch);
    }

    [Fact]
    public void Apply_WithoutFreezes_ResetsAfterOneMissedDay()
    {
        var profile = CreateProfile(streak: 3, lastStreakDay: Today.AddDays(-2), freezes: 0);

        var result = StreakRoller.Apply(profile, Today);

        Assert.NotNull(result);
        Assert.Equal("streak_reset", result!.Type);
        Assert.Equal(1, result.MissedDays);
        Assert.Equal(0, result.FreezesUsed);
        Assert.Equal(0, profile.Streak);
        Assert.Null(profile.LastStreakDay);
    }

    private static UserProfile CreateProfile(int streak, DateOnly? lastStreakDay, int freezes) =>
        new()
        {
            UserId = "user-1",
            Streak = streak,
            LastStreakDay = lastStreakDay,
            StreakFreezeCount = freezes,
            UpdatedAt = DateTime.UnixEpoch
        };
}

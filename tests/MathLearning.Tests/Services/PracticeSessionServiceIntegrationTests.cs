using MathLearning.Api.Services;
using MathLearning.Application.DTOs.Practice;
using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public class PracticeSessionServiceIntegrationTests
{
    [Fact]
    public async Task StartAnswerComplete_PersistsAttemptMasteryAndDailyActivity()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var scheduler = new FakeWeaknessScheduler();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = BuildService(db, scheduler, cache);

        var start = await sut.StartSessionAsync(
            "1",
            new StartPracticeSessionRequest(
                UserId: "should_be_ignored",
                SkillNodeId: "fractions_basics",
                TopicId: 1,
                SubtopicId: 1,
                TargetQuestions: 1,
                PreferredDifficulty: "medium"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, start.SessionId);
        Assert.NotNull(start.Question);

        var correctOption = await GetCorrectOptionIdAsync(db, start.Question!.Id);
        var answer = await sut.SubmitAnswerAsync(
            "1",
            start.SessionId,
            new SubmitPracticeAnswerRequest(start.Question.Id, correctOption.ToString(), 12000),
            CancellationToken.None);

        Assert.True(answer.IsCorrect);
        Assert.Null(answer.NextQuestion);

        var completed = await sut.CompleteSessionAsync("1", start.SessionId, CancellationToken.None);
        Assert.Equal("Completed", completed.Status);
        Assert.True(completed.XpEarned > 0);
        Assert.True(completed.FinalMastery >= 0m);

        var attempts = await db.QuizAttempts.CountAsync();
        Assert.Equal(1, attempts);

        var mastery = await db.MasteryStates.FirstOrDefaultAsync(x => x.UserId == "1");
        Assert.NotNull(mastery);

        var daily = await db.UserDailyStats.FirstOrDefaultAsync(x => x.UserId == "1");
        Assert.NotNull(daily);
        Assert.True(daily!.Completed);

    }

    [Fact]
    public async Task SubmitAnswer_UsesDifficultyBasedXpRules()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var scheduler = new FakeWeaknessScheduler();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = BuildService(db, scheduler, cache);

        var start = await sut.StartSessionAsync(
            "1",
            new StartPracticeSessionRequest(
                UserId: null,
                SkillNodeId: null,
                TopicId: 1,
                SubtopicId: 1,
                TargetQuestions: 1,
                PreferredDifficulty: "easy"),
            CancellationToken.None);

        Assert.NotNull(start.Question);

        var correctOption = await GetCorrectOptionIdAsync(db, start.Question!.Id);
        var answer = await sut.SubmitAnswerAsync(
            "1",
            start.SessionId,
            new SubmitPracticeAnswerRequest(start.Question.Id, correctOption.ToString(), 5000),
            CancellationToken.None);

        var expectedXp = start.Question.Difficulty switch
        {
            "easy" => 5,
            "medium" => 8,
            "hard" => 12,
            _ => 8
        };

        Assert.True(answer.IsCorrect);
        Assert.Equal(expectedXp, answer.XpEarned);
    }

    [Fact]
    public async Task DifficultyAdapts_DownAfterTwoIncorrect()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var scheduler = new FakeWeaknessScheduler();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = BuildService(db, scheduler, cache);

        db.MasteryStates.Add(new MasteryState
        {
            Id = Guid.NewGuid(),
            UserId = "1",
            TopicId = 1,
            SubtopicId = 1,
            PL = 0.85m,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var start = await sut.StartSessionAsync(
            "1",
            new StartPracticeSessionRequest(
                UserId: null,
                SkillNodeId: "algebra_1",
                TopicId: 1,
                SubtopicId: 1,
                TargetQuestions: 3,
                PreferredDifficulty: null),
            CancellationToken.None);

        Assert.Equal("hard", start.RecommendedDifficulty);
        Assert.NotNull(start.Question);

        var firstWrong = await GetWrongOptionIdAsync(db, start.Question!.Id);
        var firstAnswer = await sut.SubmitAnswerAsync(
            "1",
            start.SessionId,
            new SubmitPracticeAnswerRequest(start.Question.Id, firstWrong.ToString(), 6000),
            CancellationToken.None);
        Assert.False(firstAnswer.IsCorrect);
        Assert.NotNull(firstAnswer.NextQuestion);

        var secondWrong = await GetWrongOptionIdAsync(db, firstAnswer.NextQuestion!.Id);
        db.ChangeTracker.Clear();
        var secondAnswer = await sut.SubmitAnswerAsync(
            "1",
            start.SessionId,
            new SubmitPracticeAnswerRequest(firstAnswer.NextQuestion.Id, secondWrong.ToString(), 6000),
            CancellationToken.None);
        Assert.False(secondAnswer.IsCorrect);

        var session = await db.PracticeSessions.FirstAsync(x => x.Id == start.SessionId);
        Assert.NotEqual("hard", session.RecommendedDifficulty);
    }

    private static PracticeSessionService BuildService(
        Infrastructure.Persistance.ApiDbContext db,
        IWeaknessAnalysisScheduler scheduler,
        IMemoryCache cache)
    {
        var bkt = new BktService(cache);
        var selector = new EfQuestionSelector(db);
        var analyticsUpdater = new PracticeAnalyticsUpdater(
            db,
            scheduler,
            NullLogger<PracticeAnalyticsUpdater>.Instance);
        var adaptiveAnalytics = new AdaptiveAnalyticsService(NullLogger<AdaptiveAnalyticsService>.Instance);
        var backgroundJobs = new PracticeBackgroundJobs(
            new FakeBackgroundJobClient(),
            analyticsUpdater,
            adaptiveAnalytics,
            NullLogger<PracticeBackgroundJobs>.Instance);

        return new PracticeSessionService(
            db,
            selector,
            bkt,
            analyticsUpdater,
            backgroundJobs,
            adaptiveAnalytics,
            new NoOpAnswerPatternAntiCheatService(),
            NullLogger<PracticeSessionService>.Instance);
    }

    private static async Task<int> GetCorrectOptionIdAsync(Infrastructure.Persistance.ApiDbContext db, int questionId)
    {
        var question = await db.Questions
            .Include(x => x.Options)
            .FirstAsync(x => x.Id == questionId);
        return question.Options.First(x => x.IsCorrect).Id;
    }

    private static async Task<int> GetWrongOptionIdAsync(Infrastructure.Persistance.ApiDbContext db, int questionId)
    {
        var question = await db.Questions
            .Include(x => x.Options)
            .FirstAsync(x => x.Id == questionId);
        return question.Options.First(x => !x.IsCorrect).Id;
    }

    private sealed class FakeWeaknessScheduler : IWeaknessAnalysisScheduler
    {
        public Guid? LastEnqueued { get; private set; }

        public void Enqueue(Guid userId)
        {
            LastEnqueued = userId;
        }
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => Guid.NewGuid().ToString("N");

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }
}

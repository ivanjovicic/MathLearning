using MathLearning.Application.Content;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Services;
using MathLearning.Application.Validators;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Performance;
using MathLearning.Infrastructure.Services.QuestionAuthoring;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public class QuestionAuthoringVersionConcurrencyTests
{
    [Fact]
    public async Task ConcurrentSaveDraftAsync_AllocatesUniqueSequentialDraftVersions()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateSharedInMemoryOptions(dbName);

        await using (var setup = new ApiDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            await TestDbContextFactory.SeedAsync(setup);
        }

        var request = new SaveQuestionDraftRequest(CreateValidRequest(questionId: 1));
        var save1 = SaveDraftAsync(options, request, "author-a");
        var save2 = SaveDraftAsync(options, request, "author-b");
        await Task.WhenAll(save1, save2);

        await using var verify = new ApiDbContext(options);
        var drafts = await verify.QuestionDrafts
            .Where(x => x.QuestionId == 1)
            .OrderBy(x => x.DraftVersion)
            .ToListAsync();

        Assert.Equal(2, drafts.Count);
        Assert.Equal([1, 2], drafts.Select(x => x.DraftVersion).ToArray());
        Assert.Equal(2, drafts.Select(x => x.Id).Distinct().Count());

        var question = await verify.Questions.FirstAsync(x => x.Id == 1);
        Assert.NotNull(question.CurrentDraftId);
        Assert.Contains(question.CurrentDraftId.Value, drafts.Select(x => x.Id));
    }

    [Fact]
    public async Task ConcurrentPublishAsync_CreatesSinglePublishedVersionPerDraft()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateSharedInMemoryOptions(dbName);
        Guid draftId;

        await using (var setup = new ApiDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            await TestDbContextFactory.SeedAsync(setup);

            var service = CreateAuthoringService(setup);
            var save = await service.SaveDraftAsync(
                new SaveQuestionDraftRequest(CreateValidRequest(questionId: 1)),
                "author-1",
                CancellationToken.None);
            draftId = save.DraftId;
        }

        var publish1 = PublishAsync(options, draftId, "publisher-a");
        var publish2 = PublishAsync(options, draftId, "publisher-b");
        var results = await Task.WhenAll(publish1, publish2);

        Assert.All(results, result => Assert.True(result.Published));

        await using var verify = new ApiDbContext(options);
        var versionsForDraft = await verify.QuestionVersions
            .Where(x => x.SourceDraftId == draftId)
            .ToListAsync();

        Assert.Single(versionsForDraft);

        var versionsForQuestion = await verify.QuestionVersions
            .Where(x => x.QuestionId == 1)
            .ToListAsync();

        Assert.Single(versionsForQuestion);
        Assert.Equal(versionsForDraft[0].Id, results[0].VersionId);
        Assert.Equal(versionsForDraft[0].Id, results[1].VersionId);
        Assert.Equal(versionsForDraft[0].VersionNumber, results[0].VersionNumber);
        Assert.Equal(versionsForDraft[0].VersionNumber, results[1].VersionNumber);
    }

    private static DbContextOptions<ApiDbContext> CreateSharedInMemoryOptions(string dbName)
        => new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

    private static async Task<SaveQuestionDraftResponse> SaveDraftAsync(
        DbContextOptions<ApiDbContext> options,
        SaveQuestionDraftRequest request,
        string actorUserId)
    {
        await using var db = new ApiDbContext(options);
        var service = CreateAuthoringService(db);
        return await service.SaveDraftAsync(request, actorUserId, CancellationToken.None);
    }

    private static async Task<PublishQuestionResponse> PublishAsync(
        DbContextOptions<ApiDbContext> options,
        Guid draftId,
        string actorUserId)
    {
        await using var db = new ApiDbContext(options);
        var service = CreateAuthoringService(db);
        return await service.PublishAsync(
            new PublishQuestionRequest(draftId, "concurrent publish"),
            actorUserId,
            CancellationToken.None);
    }

    private static QuestionAuthoringRequest CreateValidRequest(int? questionId)
        => new(
            questionId,
            "Koliko je $1+1$?",
            "multiple_choice",
            "$2$",
            "Saberi brojeve.",
            2,
            1,
            1,
            [
                new QuestionAuthoringOptionDto(1, "$2$", true),
                new QuestionAuthoringOptionDto(2, "$3$", false),
                new QuestionAuthoringOptionDto(3, "$4$", false)
            ],
            [
                new QuestionHintDto("formula", "$a+b$"),
                new QuestionHintDto("clue", "Pogledaj sabiranje."),
                new QuestionHintDto("full", "Rezultat je 2.")
            ],
            [new StepExplanationAuthoringDto(1, "$1+1=2$", null, false)],
            "test");

    private static MathQuestionAuthoringService CreateAuthoringService(ApiDbContext db)
    {
        var cache = new HybridCacheService(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 }),
            NullLogger<HybridCacheService>.Instance);
        var sanitizer = new MathContentSanitizer();

        return new MathQuestionAuthoringService(
            db,
            NullLogger<MathQuestionAuthoringService>.Instance,
            cache,
            new MathContentLinter(),
            new LatexValidationService(),
            new MathNormalizationService(),
            new MathEquivalenceService(),
            new StepExplanationValidationService(),
            new DifficultyEstimationService(),
            new QuestionPreviewService(),
            new QuestionPublishGuardService(),
            new NoOpQuestionAutoHintGenerator(NullLogger<NoOpQuestionAutoHintGenerator>.Instance),
            new QuestionAuthoringService(
                sanitizer,
                new QuestionAuthoringRequestValidator(),
                NullLogger<QuestionAuthoringService>.Instance),
            sanitizer);
    }
}

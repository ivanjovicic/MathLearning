using MathLearning.Api.Services;
using MathLearning.Application.DTOs.Explanations;
using MathLearning.Application.Services;
using MathLearning.Tests.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public class StepExplanationServiceIntegrationTests
{
    [Fact]
    public async Task GenerateAsync_FractionProblem_ReturnsStructuredStepsAndConceptStep()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var response = await service.GenerateAsync(new GenerateExplanationRequest(
            ProblemId: null,
            ProblemText: "3/4 + 1/4",
            StudentAnswer: null,
            ExpectedAnswer: null,
            Topic: "Fractions",
            Subtopic: "Addition",
            Grade: 5,
            Difficulty: "Easy",
            Language: "en",
            EnableAiTutorEnhancement: true,
            ForceRefresh: false));

        Assert.NotEmpty(response.Steps);
        Assert.Contains("1", response.Steps[^1].Text, StringComparison.Ordinal);
        Assert.Contains(response.Steps, step => step.ExplanationType == "CONCEPT_CLARIFICATION");
        Assert.Contains(response.FormulaReferences, formula => formula.Id == "fraction_addition_rule");
    }

    [Fact]
    public async Task AnalyzeMistakeAsync_DenominatorAddition_ReturnsTargetedMistake()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var response = await service.AnalyzeMistakeAsync(new MistakeAnalysisRequest(
            ProblemId: null,
            ProblemText: "3/4 + 1/4",
            StudentAnswer: "4/8",
            ExpectedAnswer: null,
            Topic: "Fractions",
            Subtopic: "Addition",
            Grade: 5,
            Difficulty: "Easy",
            Language: "en",
            ForceRefresh: false));

        Assert.NotEmpty(response.Mistakes);
        Assert.Contains(response.Mistakes, m => m.MistakeType == "FRACTION_DENOMINATOR_ADDITION");
        Assert.Contains(response.Steps, s => s.StepType == "MISTAKE_EXPLANATION");
    }

    [Fact]
    public async Task GenerateAsync_RepeatedRequest_ReturnsCachedPayloadOnSecondCall()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var request = new GenerateExplanationRequest(
            ProblemId: null,
            ProblemText: "2x + 3 = 11",
            StudentAnswer: null,
            ExpectedAnswer: null,
            Topic: "Algebra",
            Subtopic: "Linear equations",
            Grade: 6,
            Difficulty: "Medium",
            Language: "en",
            EnableAiTutorEnhancement: false,
            ForceRefresh: false);

        var first = await service.GenerateAsync(request);
        var second = await service.GenerateAsync(request);

        Assert.False(first.ServedFromCache);
        Assert.True(second.ServedFromCache);
        Assert.Equal(first.ProblemHash, second.ProblemHash);
    }

    private static IStepExplanationService CreateService(MathLearning.Infrastructure.Persistance.ApiDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }));
        services.AddLogging();

        var provider = services.BuildServiceProvider();

        var graphEngine = new MathReasoningGraphEngine();
        var formulaService = new FormulaReferenceService(db);
        var cache = new ExplanationCacheService(
            provider.GetRequiredService<IMemoryCache>(),
            db,
            provider,
            NullLogger<ExplanationCacheService>.Instance);
        var mistakeDetector = new CommonMistakeDetector(db);
        var generator = new StepExplanationGenerator();
        var aiTutor = new AiTutorEnhancer();

        return new StepExplanationService(
            db,
            graphEngine,
            generator,
            mistakeDetector,
            formulaService,
            aiTutor,
            cache,
            NullLogger<StepExplanationService>.Instance);
    }
}

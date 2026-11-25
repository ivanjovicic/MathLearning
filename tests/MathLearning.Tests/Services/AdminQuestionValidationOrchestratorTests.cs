using MathLearning.Admin.Services;
using MathLearning.Application.Content;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Validators;
using MathLearning.Infrastructure.Services.QuestionAuthoring;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public class AdminQuestionValidationOrchestratorTests
{
    [Fact]
    public async Task LegacyAdminPreview_ReportsSameMissingApprovalAsPublishGuard()
    {
        var sanitizer = new MathContentSanitizer();
        var orchestrator = new AdminQuestionValidationOrchestrator(
            new MathContentLinter(),
            new LatexValidationService(),
            new MathNormalizationService(),
            new MathEquivalenceService(),
            new StepExplanationValidationService(),
            new DifficultyEstimationService(),
            new QuestionPreviewService(),
            new QuestionPublishGuardService(),
            sanitizer,
            new MathVisualContentPolicy([], [1]));
        var request = new QuestionAuthoringRequest(
            null, "Koliko je $7 \\times 8$?", "multiple_choice", "56", "Sedam redova po osam.",
            2, 1, 1, [new(null, "56", true), new(null, "54", false)], [],
            [new(1, "$5 \\times 8 = 40$", null, false), new(2, "$2 \\times 8 = 16$", null, false), new(3, "$40 + 16 = 56$", null, true)],
            ContentPackId: MathVisualContentPolicy.PackId,
            MathVisualPlan: new(1, "splitFive", 1, "sr", "multiply", "product", 7, 8, [5, 2]),
            ContentLocale: "sr");

        var result = await orchestrator.ValidateAsync(request);

        Assert.False(result.Summary.CanPublish);
        Assert.Contains(result.Summary.Issues, x => x.RuleId == "mathvisual.review_missing");
    }
}

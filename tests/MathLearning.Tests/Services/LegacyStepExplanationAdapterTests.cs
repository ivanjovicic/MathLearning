using MathLearning.Api.Services;
using MathLearning.Domain.Entities;

namespace MathLearning.Tests.Services;

public class LegacyStepExplanationAdapterTests
{
    [Fact]
    public void GetSteps_ForRecognizedProblem_UsesReasoningGraphOutput()
    {
        var question = new Question("3/4 + 1/4", 1, 1);
        question.SetCorrectAnswer("1");

        var adapter = new LegacyStepExplanationAdapter(
            new MathReasoningGraphEngine(),
            new StepExplanationGenerator());

        var steps = adapter.GetSteps(question, "en");

        Assert.NotEmpty(steps);
        Assert.Contains(steps, step => step.Text.Contains("common denominator", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Text.Contains("final result", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetSteps_ForUnknownProblem_FallsBackToLegacyStepEngine()
    {
        var question = new Question("Objasni postupak rešavanja.", 2, 1, "Kreni od datog objašnjenja.");

        var adapter = new LegacyStepExplanationAdapter(
            new MathReasoningGraphEngine(),
            new StepExplanationGenerator());

        var steps = adapter.GetSteps(question, "sr");

        Assert.Single(steps);
        Assert.Equal("Kreni od datog objašnjenja.", steps[0].Text);
    }
}

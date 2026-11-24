using MathLearning.Api.Services;
using MathLearning.Domain.Entities;

namespace MathLearning.Tests.Services;

public class LegacyStepExplanationAdapterTests
{
    [Fact]
    public void GetSteps_ForRecognizedProblem_UsesReasoningGraphOutput()
    {
        var question = new Question("3/4 + 1/4", 1, 1);
        question.ReplaceOptions([
            new QuestionOption("1", true, order: 1),
            new QuestionOption("2", false, order: 2)
        ]);
        question.SetCorrectAnswer("999");

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

    [Theory]
    [InlineData("Koliko je 7 × 8?", true)]
    [InlineData("7 × 8", false)]
    public void GetSteps_UnreviewedSevenByEight_CharacterizesRouteDifference(
        string text,
        bool usesLegacyGroups)
    {
        var question = new Question(text, 1, 1);
        var adapter = new LegacyStepExplanationAdapter(
            new MathReasoningGraphEngine(),
            new StepExplanationGenerator());

        var steps = adapter.GetSteps(question, "sr");

        Assert.NotEmpty(steps);
        Assert.Equal(usesLegacyGroups, steps.Any(step =>
            step.Text == "7 × 8 znači: saberi 7, 8 puta"));
        Assert.Contains(steps, step => step.Text.Contains("56", StringComparison.Ordinal));
    }

    [Fact]
    public void GetSteps_AuthoredStepsTakePriorityOverBothGeneratedPaths()
    {
        var question = new Question("Koliko je 7 × 8?", 1, 1);
        question.ReplaceSteps([new QuestionStep(0, 1, "Odobreni korak", null, true)]);
        var adapter = new LegacyStepExplanationAdapter(
            new MathReasoningGraphEngine(),
            new StepExplanationGenerator());

        var step = Assert.Single(adapter.GetSteps(question, "sr"));

        Assert.Equal("Odobreni korak", step.Text);
    }
}

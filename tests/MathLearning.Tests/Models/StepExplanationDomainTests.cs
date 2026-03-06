using MathLearning.Domain.Explanations;

namespace MathLearning.Tests.Models;

public class StepExplanationDomainTests
{
    [Fact]
    public void StepExplanation_WhenTextIsEmpty_Throws()
    {
        var context = new MathContext("Fractions", "Simplification", 5, DifficultyLevel.Easy);

        Assert.Throws<ArgumentException>(() => new StepExplanation(
            1,
            "",
            StepType.Calculation,
            ExplanationType.Normal,
            false,
            null,
            DifficultyLevel.Easy,
            context));
    }

    [Fact]
    public void Hint_WhenTextExceedsLimit_Throws()
    {
        var text = new string('a', Hint.MaxTextLength + 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hint(text, HintType.General));
    }
}

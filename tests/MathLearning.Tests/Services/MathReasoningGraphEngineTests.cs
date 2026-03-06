using MathLearning.Api.Services;
using MathLearning.Application.DTOs.Explanations;
using MathLearning.Domain.Explanations;

namespace MathLearning.Tests.Services;

public class MathReasoningGraphEngineTests
{
    private readonly MathReasoningGraphEngine _engine = new();

    [Fact]
    public void Build_ForFractionAddition_ProducesSimplifiedFinalNode()
    {
        var descriptor = new MathProblemDescriptor(
            null,
            "3/4 + 1/4",
            null,
            null,
            new MathContext("Fractions", "Addition", 5, DifficultyLevel.Easy),
            "en");

        var graph = _engine.Build(descriptor);

        Assert.Equal("1", graph.Nodes.Last().Expression);
        Assert.Contains(graph.Nodes, x => x.RuleApplied == ReasoningRule.AddFractions);
        Assert.Contains(graph.Nodes, x => x.RuleApplied == ReasoningRule.AddNumerators);
    }

    [Fact]
    public void Build_ForLinearEquation_ProducesSolvedVariable()
    {
        var descriptor = new MathProblemDescriptor(
            null,
            "2x + 3 = 11",
            null,
            null,
            new MathContext("Algebra", "Linear equations", 6, DifficultyLevel.Medium),
            "en");

        var graph = _engine.Build(descriptor);

        Assert.Equal("x = 4", graph.Nodes.Last().Expression);
        Assert.Contains(graph.Nodes, x => x.RuleApplied == ReasoningRule.NormalizeEquation);
        Assert.Contains(graph.Nodes, x => x.RuleApplied == ReasoningRule.IsolateVariable);
    }
}

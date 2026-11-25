using MathLearning.Application.DTOs.Questions;
using MathLearning.Infrastructure.Services.QuestionAuthoring;

namespace MathLearning.Tests.Services;

public class MathVisualContentPolicyTests
{
    [Theory]
    [InlineData("7 × 8", 7, 8)]
    [InlineData("Koliko je 7 × 8?", 7, 8)]
    [InlineData("7 × 8 = ?", 7, 8)]
    [InlineData("8 × 7", 8, 7)]
    [InlineData("2 × 2", 2, 2)]
    [InlineData("5 × 8", 5, 8)]
    [InlineData("6 × 7", 6, 7)]
    [InlineData("9 × 9", 9, 9)]
    public void ApprovedProductPromptForms_PreserveOrderedOperands(string prompt, int rows, int columns)
    {
        Assert.True(MathVisualContentPolicy.TryParseProductPrompt(prompt, out var actualRows, out var actualColumns));
        Assert.Equal(rows, actualRows);
        Assert.Equal(columns, actualColumns);
    }

    [Fact]
    public void All64OrderedPairs_And36CommutativeFamilies_HaveValidButDistinctGeometry()
    {
        var policy = new MathVisualContentPolicy(["reviewed-fixture"], [1]);
        var families = new HashSet<(int, int)>();
        var ordered = new HashSet<(int, int)>();

        for (var rows = 2; rows <= 9; rows++)
        for (var columns = 2; columns <= 9; columns++)
        {
            var request = Request(rows, columns);
            var issues = policy.Validate(request, "reviewed-fixture");
            Assert.Empty(issues);
            Assert.Equal(rows, request.MathVisualPlan!.Groups.Sum());
            Assert.Equal(rows * columns, request.MathVisualPlan.Groups.Sum(group => group * columns));
            ordered.Add((rows, columns));
            families.Add((Math.Min(rows, columns), Math.Max(rows, columns)));
        }

        Assert.Equal(64, ordered.Count);
        Assert.Equal(36, families.Count);
        Assert.NotEqual(Request(7, 8).MathVisualPlan!.Rows, Request(8, 7).MathVisualPlan!.Rows);
    }

    [Theory]
    [InlineData("? × 8 = 56")]
    [InlineData("7 × ? = 56")]
    [InlineData("56 ÷ 8")]
    [InlineData("0 × 8")]
    [InlineData("-7 × 8")]
    [InlineData("7.5 × 8")]
    public void UnsupportedOrAnswerRevealingShapes_AreRejected(string prompt)
    {
        var request = Request(7, 8) with { Text = prompt };
        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.prompt_unsupported");
    }

    [Theory]
    [InlineData(7, 8, 5, 3)]
    [InlineData(7, 8, 4, 2)]
    [InlineData(2, 2, 5, -3)]
    public void WrongGroups_AreRejected(int rows, int columns, int first, int second)
    {
        var request = Request(rows, columns);
        request = request with { MathVisualPlan = request.MathVisualPlan! with { Groups = [first, second] } };
        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.geometry_mismatch");
    }

    [Fact]
    public void OverflowingGroups_AreRejectedWithoutThrowing()
    {
        var request = Request(7, 8);
        request = request with
        {
            MathVisualPlan = request.MathVisualPlan! with { Groups = [int.MaxValue, int.MaxValue] }
        };

        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");

        Assert.Contains(issues, x => x.RuleId == "mathvisual.geometry_mismatch");
    }

    [Fact]
    public void MalformedNullStepText_IsRejectedWithoutThrowing()
    {
        var request = Request(7, 8) with { Steps = [new(1, null!, null, false)] };
        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.invalid_snapshot");
    }

    [Fact]
    public void CorrectTotalWithTransposedFactors_StillFails()
    {
        var request = Request(7, 8) with
        {
            MathVisualPlan = Request(8, 7).MathVisualPlan
        };
        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.prompt_mismatch");
    }

    [Theory]
    [InlineData("neighbor")]
    [InlineData("double")]
    [InlineData("commute")]
    [InlineData("missingFactor")]
    [InlineData("<script>alert(1)</script>")]
    public void UnapprovedTemplate_IsRejected(string template)
    {
        var request = Request(7, 8);
        request = request with { MathVisualPlan = request.MathVisualPlan! with { TemplateId = template } };
        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.schema_unsupported");
    }

    [Fact]
    public void ContradictoryAuthoredEquation_IsRejected()
    {
        var request = Request(7, 8) with
        {
            Steps = [new(1, "5 × 8 = 40", null, false), new(2, "2 × 8 = 15", null, false), new(3, "40 + 16 = 56", null, false)]
        };
        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.steps_mismatch");
    }

    [Fact]
    public void ServerDesignatedSubtopic_CannotDropPackLabel()
    {
        var request = Request(7, 8) with { ContentPackId = null, MathVisualPlan = null };
        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.pack_required");
    }

    [Fact]
    public void ApprovedTextOnlyContent_StillChecksCorrectAnswer()
    {
        var request = Request(7, 8) with { MathVisualPlan = null, CorrectAnswer = "55" };
        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.answer_mismatch");
    }

    [Theory]
    [InlineData("56 ÷ 8 = 8")]
    [InlineData("7 × 8 = 56.1")]
    [InlineData("7 × 8 znači: saberi 7, 8 puta")]
    public void ExtraContradictoryExplanation_IsRejected(string contradiction)
    {
        var request = Request(7, 8) with { Explanation = contradiction };
        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.contradictory_equation");
    }

    [Theory]
    [InlineData("hint")]
    [InlineData("semantics")]
    public void ContradictoryStepMetadata_IsRejected(string field)
    {
        var request = Request(7, 8);
        var first = request.Steps[0] with
        {
            Hint = field == "hint" ? "5 × 8 = 41" : null,
            SemanticsAltText = field == "semantics" ? "5 × 8 = 41" : null
        };
        request = request with { Steps = [first, .. request.Steps.Skip(1)] };
        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.contradictory_equation");
    }

    [Theory]
    [InlineData("<svg onload=alert(1)></svg>")]
    [InlineData("![array](https://example.org/array.svg)")]
    [InlineData("[open](javascript:alert(1))")]
    [InlineData("<script>alert(1)</script>")]
    public void ExecutableOrExternalTeachingMarkup_IsRejected(string markup)
    {
        var request = Request(7, 8) with { Explanation = markup };
        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.markup_unsupported");
    }

    [Fact]
    public void PreAnswerHintOrSemanticAnswer_IsRejected()
    {
        var request = Request(7, 8) with
        {
            Hints = [new("full", "56")],
            SemanticsAltText = "Odgovor je 56"
        };
        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.pre_hint_unsupported");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.pre_semantics_reveal");
    }

    [Fact]
    public void AnswerBearingOptionAccessibilityText_IsRejected()
    {
        var request = Request(7, 8) with
        {
            Options = [new(null, "56", true, SemanticsAltText: "Tačan odgovor je 56"), new(null, "54", false)]
        };
        var issues = new MathVisualContentPolicy(["reviewed-fixture"], [1]).Validate(request, "reviewed-fixture");
        Assert.Contains(issues, x => x.RuleId == "mathvisual.pre_option_semantics_reveal");
    }

    private static QuestionAuthoringRequest Request(int rows, int columns)
    {
        var product = rows * columns;
        var split = rows > 5;
        var steps = split
            ? new StepExplanationAuthoringDto[]
            {
                new(1, $"5 × {columns} = {5 * columns}", null, false),
                new(2, $"{rows - 5} × {columns} = {(rows - 5) * columns}", null, false),
                new(3, $"{5 * columns} + {(rows - 5) * columns} = {product}", null, true)
            }
            : [new(1, $"{rows} × {columns} = {product}", null, true)];
        return new QuestionAuthoringRequest(
            null, $"Koliko je {rows} × {columns}?", "multiple_choice", product.ToString(),
            $"{rows} redova sa {columns} elemenata.", 2, 1, 1,
            [new(null, product.ToString(), true), new(null, (product + 1).ToString(), false)],
            [], steps,
            ContentPackId: MathVisualContentPolicy.PackId,
            MathVisualPlan: new(1, split ? "splitFive" : "directArray", 1, "sr", "multiply", "product", rows, columns,
                split ? [5, rows - 5] : [rows]),
            ContentLocale: "sr");
    }
}

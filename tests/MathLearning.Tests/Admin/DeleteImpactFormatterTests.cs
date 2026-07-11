using MathLearning.Admin.Services;

namespace MathLearning.Tests.Admin;

public sealed class DeleteImpactFormatterTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void BuildCategoryDeleteMessage_WithoutDependants_AllowsPermanentDelete(int dependentQuestionsCount)
    {
        var message = DeleteImpactFormatter.BuildCategoryDeleteMessage("Algebra", dependentQuestionsCount);

        Assert.Equal("Delete category 'Algebra' permanently?", message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    public void BuildCategoryDeleteMessage_WithDependants_ExplainsWhyDeleteIsBlocked(int dependentQuestionsCount)
    {
        var message = DeleteImpactFormatter.BuildCategoryDeleteMessage("Geometry", dependentQuestionsCount);

        Assert.Equal(
            $"Category 'Geometry' contains {dependentQuestionsCount} question(s). Delete is blocked until questions are moved or removed.",
            message);
    }

    [Fact]
    public void BuildQuestionDeleteMessage_IncludesQuestionAndDependentCounts()
    {
        var message = DeleteImpactFormatter.BuildQuestionDeleteMessage(
            questionId: 42,
            optionsCount: 4,
            stepsCount: 3);

        Assert.Equal(
            "Delete question #42 permanently? This also removes 4 option(s) and 3 step(s).",
            message);
    }
}

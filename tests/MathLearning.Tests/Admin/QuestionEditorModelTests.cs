using MathLearning.Admin.Models;

namespace MathLearning.Tests.Admin;

public sealed class QuestionEditorModelTests
{
    [Fact]
    public void CreateDefaultOptions_ReturnsTwoOptionsWithFirstMarkedCorrect()
    {
        var options = QuestionEditorModel.CreateDefaultOptions();

        Assert.Collection(
            options,
            first =>
            {
                Assert.True(first.IsCorrect);
                Assert.Equal(string.Empty, first.Text);
            },
            second =>
            {
                Assert.False(second.IsCorrect);
                Assert.Equal(string.Empty, second.Text);
            });
    }

    [Fact]
    public void CreateDefaultOptions_EachCallReturnsIndependentListAndItems()
    {
        var first = QuestionEditorModel.CreateDefaultOptions();
        var second = QuestionEditorModel.CreateDefaultOptions();

        Assert.NotSame(first, second);
        Assert.NotSame(first[0], second[0]);
        Assert.NotSame(first[1], second[1]);

        first[0].Text = "changed";
        first.Add(new QuestionOptionEditorModel { Text = "extra" });

        Assert.Equal(string.Empty, second[0].Text);
        Assert.Equal(2, second.Count);
    }

    [Fact]
    public void NewQuestionEditorModel_UsesDefaultMultipleChoiceShape()
    {
        var model = new QuestionEditorModel();

        Assert.Equal("multiple_choice", model.Type);
        Assert.Equal(2, model.Options.Count);
        Assert.True(model.Options[0].IsCorrect);
        Assert.False(model.Options[1].IsCorrect);
        Assert.Empty(model.Steps);
    }
}

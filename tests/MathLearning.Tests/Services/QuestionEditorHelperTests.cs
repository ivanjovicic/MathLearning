using System.Reflection;
using MathLearning.Admin.Models;
using MathLearning.Application.Content;
using MathLearning.Domain.Entities;

namespace MathLearning.Tests.Services;

public class QuestionEditorHelperTests
{
    [Fact]
    public void MapQuestionToModel_FallsBackToLegacyCorrectAnswerText()
    {
        var helper = new QuestionEditorHelper(new MathContentSanitizer());
        var question = new Question("Koliko je 2+2?", 1, 1);
        question.SetType("multiple_choice");
        question.SetSubtopic(1);
        question.SetCorrectAnswer("4");
        question.ReplaceOptions(
        [
            CreatePersistedOption(10, "3", false, 1),
            CreatePersistedOption(11, "4", false, 2)
        ]);

        var model = new QuestionEditorModel();

        helper.MapQuestionToModel(question, model);

        Assert.False(model.Options[0].IsCorrect);
        Assert.True(model.Options[1].IsCorrect);
    }

    [Fact]
    public void CreateModelSnapshot_ChangesWhenModelChanges()
    {
        var helper = new QuestionEditorHelper(new MathContentSanitizer());
        var model = new QuestionEditorModel
        {
            Text = "Pocetni tekst",
            CategoryId = 1,
            SubtopicId = 1
        };

        var originalSnapshot = helper.CreateModelSnapshot(model);
        model.Text = "Izmenjeni tekst";

        var updatedSnapshot = helper.CreateModelSnapshot(model);

        Assert.NotEqual(originalSnapshot, updatedSnapshot);
    }

    private static QuestionOption CreatePersistedOption(int id, string text, bool isCorrect, int order)
    {
        var option = new QuestionOption(text, isCorrect, order);
        typeof(QuestionOption)
            .GetProperty(nameof(QuestionOption.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(option, id);
        return option;
    }
}

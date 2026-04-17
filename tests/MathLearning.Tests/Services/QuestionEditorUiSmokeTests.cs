using System.Reflection;
using MathLearning.Admin.Models;

namespace MathLearning.Tests.Services;

public class QuestionEditorUiSmokeTests
{
    [Fact]
    public void AddOption_AppendsOptionToEditorModel()
    {
        var model = new QuestionEditorModel
        {
            Options =
            [
                new QuestionOptionEditorModel { Text = "A", IsCorrect = true },
                new QuestionOptionEditorModel { Text = "B", IsCorrect = false }
            ]
        };

        var editor = CreateEditorComponent(model);
        Invoke(editor, "AddOption");

        Assert.Equal(3, model.Options.Count);
        Assert.Equal(string.Empty, model.Options[^1].Text);
    }

    [Fact]
    public void MoveStep_ReordersAndNormalizesStepOrder()
    {
        var model = new QuestionEditorModel
        {
            Steps =
            [
                new QuestionStepEditorModel { Order = 1, Text = "Prvi" },
                new QuestionStepEditorModel { Order = 2, Text = "Drugi" },
                new QuestionStepEditorModel { Order = 3, Text = "Treci" }
            ]
        };

        var editor = CreateEditorComponent(model);
        Invoke(editor, "MoveStep", 0, 1);

        Assert.Equal(["Drugi", "Prvi", "Treci"], model.Steps.Select(x => x.Text).ToArray());
        Assert.Equal([1, 2, 3], model.Steps.Select(x => x.Order).ToArray());
    }

    [Fact]
    public void PreviewToggle_SourceContainsExpectedTabBindingAndBranches()
    {
        var filePath = Path.Combine(FindRepositoryRoot(), "src", "MathLearning.Admin", "Components", "QuestionEditor.razor");
        var content = File.ReadAllText(filePath);

        Assert.Contains("@bind-ActivePanelIndex=\"_activeTabIndex\"", content, StringComparison.Ordinal);
        Assert.Contains("if (_activeTabIndex == 0)", content, StringComparison.Ordinal);
        Assert.Contains("else if (_activeTabIndex == 1)", content, StringComparison.Ordinal);
        Assert.Contains("else if (_activeTabIndex == 2)", content, StringComparison.Ordinal);
        Assert.Contains("Live Preview", content, StringComparison.Ordinal);
    }

    private static object CreateEditorComponent(QuestionEditorModel model)
    {
        var editorType = Type.GetType("MathLearning.Admin.Components.QuestionEditor, MathLearning.Admin");
        Assert.NotNull(editorType);

        var editor = Activator.CreateInstance(editorType!, nonPublic: true);
        Assert.NotNull(editor);

        var modelProperty = editorType!.GetProperty("Model", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(modelProperty);
        modelProperty!.SetValue(editor, model);

        return editor!;
    }

    private static void Invoke(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(instance, args);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var markerPath = Path.Combine(directory.FullName, "src", "MathLearning.Admin", "MathLearning.Admin.csproj");
            if (File.Exists(markerPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}

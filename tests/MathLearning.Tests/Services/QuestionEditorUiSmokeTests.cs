using System.Reflection;
using Bunit;
using MathLearning.Admin.Components;
using MathLearning.Admin.Models;
using MathLearning.Application.Content;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Services;

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
    public void DefaultModel_StartsWithTwoOptionsAndFirstCorrectPlaceholder()
    {
        var model = new QuestionEditorModel();

        Assert.Equal(2, model.Options.Count);
        Assert.True(model.Options[0].IsCorrect);
        Assert.False(model.Options[1].IsCorrect);
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
    public void MarkCorrect_LeavesExactlyOneCorrectOption()
    {
        var model = new QuestionEditorModel
        {
            Options =
            [
                new QuestionOptionEditorModel { Text = "A", IsCorrect = true },
                new QuestionOptionEditorModel { Text = "B", IsCorrect = false },
                new QuestionOptionEditorModel { Text = "C", IsCorrect = false }
            ]
        };

        var editor = CreateEditorComponent(model);
        Invoke(editor, "MarkCorrect", 2);

        Assert.False(model.Options[0].IsCorrect);
        Assert.False(model.Options[1].IsCorrect);
        Assert.True(model.Options[2].IsCorrect);
        Assert.Equal(1, model.Options.Count(x => x.IsCorrect));
    }

    [Fact]
    public void MoveOption_ReordersOptionsAndKeepsCorrectFlag()
    {
        var model = new QuestionEditorModel
        {
            Options =
            [
                new QuestionOptionEditorModel { Text = "A", IsCorrect = true },
                new QuestionOptionEditorModel { Text = "B", IsCorrect = false },
                new QuestionOptionEditorModel { Text = "C", IsCorrect = false }
            ]
        };

        var editor = CreateEditorComponent(model);
        Invoke(editor, "MoveOption", 0, 1);

        Assert.Equal(["B", "A", "C"], model.Options.Select(x => x.Text).ToArray());
        Assert.True(model.Options[1].IsCorrect);
        Assert.Equal(1, model.Options.Count(x => x.IsCorrect));
    }

    [Fact]
    public void DuplicateOptionCheck_MatchesServerNormalization()
    {
        var model = new QuestionEditorModel
        {
            Options =
            [
                new QuestionOptionEditorModel { Text = "  x   + 1 ", IsCorrect = true },
                new QuestionOptionEditorModel { Text = "x + 1", IsCorrect = false }
            ]
        };

        var editor = CreateEditorComponent(model);

        Assert.True((bool)Invoke(editor, "IsDuplicateOption", 0)!);
        Assert.True((bool)Invoke(editor, "IsDuplicateOption", 1)!);
    }

    [Fact]
    public void PreviewPanel_SourceContainsStudentPreviewForAuthoringFields()
    {
        var filePath = Path.Combine(FindRepositoryRoot(), "src", "MathLearning.Admin", "Components", "QuestionEditor.razor");
        var content = File.ReadAllText(filePath);

        Assert.DoesNotContain("@bind-ActivePanelIndex", content, StringComparison.Ordinal);
        Assert.Contains("Osnovno pitanje", content, StringComparison.Ordinal);
        Assert.Contains("Odgovori i tacan odgovor", content, StringComparison.Ordinal);
        Assert.Contains("Koraci resavanja", content, StringComparison.Ordinal);
        Assert.Contains("Quiz preview", content, StringComparison.Ordinal);
        Assert.Contains("editor-stepper", content, StringComparison.Ordinal);
        Assert.Contains("wizard-step-locked", content, StringComparison.Ordinal);
        Assert.Contains("editor-save-bar", content, StringComparison.Ordinal);
        Assert.Contains("Preview", content, StringComparison.Ordinal);
        Assert.Contains("<MathPreview Content=\"@Model.Text\"", content, StringComparison.Ordinal);
        Assert.Contains("<MathPreview Content=\"@opt.Text\"", content, StringComparison.Ordinal);
        Assert.Contains("<MathPreview Content=\"@Model.Explanation\"", content, StringComparison.Ordinal);
        Assert.Contains("<MathPreview Content=\"@step.Text\"", content, StringComparison.Ordinal);
        Assert.Contains("<MathPreview Content=\"@Model.HintFormula\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void NewQuestionPage_SourceContainsAutosaveAndNavigationProtection()
    {
        var filePath = Path.Combine(FindRepositoryRoot(), "src", "MathLearning.Admin", "Pages", "Questions", "New.razor");
        var content = File.ReadAllText(filePath);

        Assert.Contains("<NavigationLock", content, StringComparison.Ordinal);
        Assert.Contains("loadDraft", content, StringComparison.Ordinal);
        Assert.Contains("saveDraft", content, StringComparison.Ordinal);
        Assert.Contains("clearDraft", content, StringComparison.Ordinal);
        Assert.Contains("ConfirmNavigationAsync", content, StringComparison.Ordinal);
    }

    [Fact]
    public void EditQuestionPage_SourceContainsAutosaveRestoreAndNavigationProtection()
    {
        var filePath = Path.Combine(FindRepositoryRoot(), "src", "MathLearning.Admin", "Pages", "Questions", "Edit.razor");
        var content = File.ReadAllText(filePath);

        Assert.Contains("<NavigationLock", content, StringComparison.Ordinal);
        Assert.Contains("loadDraft", content, StringComparison.Ordinal);
        Assert.Contains("saveDraft", content, StringComparison.Ordinal);
        Assert.Contains("clearDraft", content, StringComparison.Ordinal);
        Assert.Contains("ConfirmNavigationAsync", content, StringComparison.Ordinal);
        Assert.Contains("Validacija:", content, StringComparison.Ordinal);
    }

    [Fact]
    public void QuestionsIndex_SourceShowsStatusSignalsAndAvoidsHeavyIncludes()
    {
        var filePath = Path.Combine(FindRepositoryRoot(), "src", "MathLearning.Admin", "Pages", "Questions", "Index.razor");
        var content = File.ReadAllText(filePath);

        Assert.Contains("ValidationStatus", content, StringComparison.Ordinal);
        Assert.Contains("GetValidationStatusLabel", content, StringComparison.Ordinal);
        Assert.Contains("GetPublishStateLabel", content, StringComparison.Ordinal);
        Assert.DoesNotContain(".Include(q => q.Options)", content, StringComparison.Ordinal);
        Assert.DoesNotContain(".Include(q => q.Steps)", content, StringComparison.Ordinal);
    }

    [Fact]
    public void LatexInsertResult_UpdatesQuestionTextAndPreviewState()
    {
        var model = new QuestionEditorModel();
        var editor = CreateEditorComponent(model);

        Invoke(editor, "ApplyLatexInsertCore", new LatexInsertResult
        {
            Succeeded = true,
            FieldId = "question.text",
            Value = @"\frac{}{}"
        });

        Assert.Equal(@"\frac{}{}", model.Text);
        Assert.Equal("Tekst pitanja", GetPrivateField<string>(editor, "_selectedPreviewTitle"));
        Assert.Equal(@"\frac{}{}", GetPrivateField<string>(editor, "_selectedPreviewContent"));
    }

    [Fact]
    public void LatexInsertResult_UpdatesOptionStepAndHintFields()
    {
        var model = new QuestionEditorModel
        {
            Options =
            [
                new QuestionOptionEditorModel { Text = "A", IsCorrect = true },
                new QuestionOptionEditorModel { Text = "B", IsCorrect = false }
            ],
            Steps =
            [
                new QuestionStepEditorModel { Order = 1, Text = "Korak", Hint = "Hint" }
            ]
        };
        var editor = CreateEditorComponent(model);

        Invoke(editor, "ApplyLatexInsertCore", new LatexInsertResult
        {
            Succeeded = true,
            FieldId = "option:1:text",
            Value = @"\sqrt{}"
        });
        Invoke(editor, "ApplyLatexInsertCore", new LatexInsertResult
        {
            Succeeded = true,
            FieldId = "step:0:text",
            Value = @"x^2"
        });
        Invoke(editor, "ApplyLatexInsertCore", new LatexInsertResult
        {
            Succeeded = true,
            FieldId = "step:0:hint",
            Value = @"\leq"
        });
        Invoke(editor, "ApplyLatexInsertCore", new LatexInsertResult
        {
            Succeeded = true,
            FieldId = "question.hintFormula",
            Value = @"\pi"
        });

        Assert.Equal(@"\sqrt{}", model.Options[1].Text);
        Assert.Equal("x^2", model.Steps[0].Text);
        Assert.Equal(@"\leq", model.Steps[0].Hint);
        Assert.Equal(@"\pi", model.HintFormula);
        Assert.Equal("Lagani hint", GetPrivateField<string>(editor, "_selectedPreviewTitle"));
    }

    [Fact]
    public void LatexInsert_IgnoredWhenJsReportsFailure()
    {
        var model = new QuestionEditorModel { Text = "Original" };
        var editor = CreateEditorComponent(model);

        Invoke(editor, "ApplyLatexInsertCore", new LatexInsertResult
        {
            Succeeded = false,
            FieldId = "question.text",
            Value = @"\frac{}{}"
        });

        Assert.Equal("Original", model.Text);
    }

    [Fact]
    public async Task QuestionEditor_RendersLatexDefaultTargetOnQuestionText()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton<IMathContentSanitizer, MathContentSanitizer>();

        var model = new QuestionEditorModel();
        RenderFragment fragment = builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<QuestionEditor>(1);
            builder.AddAttribute(2, nameof(QuestionEditor.Model), model);
            builder.CloseComponent();
        };

        var cut = context.Render(fragment);

        var defaultLatexField = cut.Find("[data-latex-default='true']");
        Assert.Equal("question.text", defaultLatexField.GetAttribute("data-latex-field"));
    }

    [Fact]
    public async Task MathInputHelper_ClickSnippetRaisesInsertedResultFromJs()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Setup<int>("mudpopoverHelper.countProviders").SetResult(1);
        context.JSInterop.SetupVoid("mudPopover.initialize", _ => true).SetVoidResult();
        context.JSInterop.SetupVoid("mudPopover.connect", _ => true).SetVoidResult();
        context.JSInterop.SetupVoid("mudPopover.disconnect", _ => true).SetVoidResult();
        context.JSInterop.SetupVoid("mudPopover.dispose", _ => true).SetVoidResult();

        var module = context.JSInterop.SetupModule("/mathEditor.js");
        module.SetupVoid("registerCtrlM", _ => true).SetVoidResult();
        module.Setup<LatexInsertResult?>("insertLatexAtCursor", invocation =>
            invocation.Arguments.Count == 1
            && string.Equals(invocation.Arguments[0]?.ToString(), @"\frac{{}}{}", StringComparison.Ordinal))
            .SetResult(new LatexInsertResult
            {
                Succeeded = true,
                FieldId = "question.text",
                Value = @"\frac{}{}",
                SelectionStart = 10,
                SelectionEnd = 10
            });
        module.SetupVoid("unregisterCtrlM", _ => true).SetVoidResult();

        LatexInsertResult? inserted = null;
        RenderFragment fragment = builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MathInputHelper>(1);
            builder.AddAttribute(2, nameof(MathInputHelper.Inserted),
                EventCallback.Factory.Create<LatexInsertResult>(this, result => inserted = result));
            builder.CloseComponent();
        };

        var cut = context.Render(fragment);
        await cut.FindAll("button")
            .First(button => button.TextContent.Contains(@"\frac", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

        Assert.NotNull(inserted);
        Assert.Equal("question.text", inserted!.FieldId);
        Assert.Equal(@"\frac{}{}", inserted.Value);
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

    private static object? Invoke(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!.Invoke(instance, args);
    }

    private static T? GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T?)field!.GetValue(instance);
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

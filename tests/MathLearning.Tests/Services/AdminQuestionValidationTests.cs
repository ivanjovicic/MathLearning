using MathLearning.Admin.Data;
using MathLearning.Admin.Models;
using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MathLearning.Tests.Services;

public class AdminQuestionValidationTests
{
    [Fact]
    public void Validate_CollectsMultipleErrors_WhenModelIsInvalid()
    {
        var model = new QuestionEditorModel
        {
            Type = "multiple_choice",
            Text = "a",
            CategoryId = 0,
            SubtopicId = 0,
            Options =
            [
                new() { Text = "A", IsCorrect = false },
                new() { Text = "A", IsCorrect = false }
            ]
        };

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Contains(errors, error => error.Contains("najmanje 4 karaktera", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("kategoriju", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("podtemu", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Oznacite tacan odgovor.", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("identican tekst", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RequiresCorrectFilledOption_WhenBlankOptionIsMarkedCorrect()
    {
        var model = new QuestionEditorModel
        {
            Type = "multiple_choice",
            Text = "Pitanje",
            CategoryId = 1,
            SubtopicId = 1,
            Options =
            [
                new() { Text = string.Empty, IsCorrect = true },
                new() { Text = "Opcija A", IsCorrect = false },
                new() { Text = "Opcija B", IsCorrect = false }
            ]
        };

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Contains(errors, error => error == "Oznaceni tacan odgovor mora biti popunjena opcija.");
    }

    [Fact]
    public void Validate_RejectsMultipleCorrectAnswers()
    {
        var model = new QuestionEditorModel
        {
            Type = "multiple_choice",
            Text = "Pitanje",
            CategoryId = 1,
            SubtopicId = 1,
            Options =
            [
                new() { Text = "Opcija A", IsCorrect = true },
                new() { Text = "Opcija B", IsCorrect = true }
            ]
        };

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Contains(errors, error => error.Contains("samo jedan tacan odgovor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_EnforcesFieldLengthLimits()
    {
        var model = new QuestionEditorModel
        {
            Type = "multiple_choice",
            Text = new string('Q', QuestionEditorFieldLimits.QuestionTextMaxLength + 1),
            Explanation = new string('E', QuestionEditorFieldLimits.ExplanationMaxLength + 1),
            CategoryId = 1,
            SubtopicId = 1,
            Options =
            [
                new() { Text = new string('A', QuestionEditorFieldLimits.OptionTextMaxLength + 1), IsCorrect = true },
                new() { Text = "Opcija B", IsCorrect = false }
            ],
            Steps =
            [
                new() { Order = 1, Text = new string('S', QuestionEditorFieldLimits.StepTextMaxLength + 1) }
            ]
        };

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Contains(errors, error => error.Contains("Tekst pitanja ne moze imati vise od", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Objasnjenje ne moze imati vise od", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Tekst opcije ne moze imati vise od", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Tekst koraka 1 ne moze imati vise od", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_OpenAnswer_RequiresCorrectAnswerAndLimit()
    {
        var missingAnswerModel = new QuestionEditorModel
        {
            Type = "open_answer",
            Text = "Pitanje",
            CategoryId = 1,
            SubtopicId = 1,
            CorrectAnswer = string.Empty
        };

        var missingAnswerErrors = QuestionEditorValidation.Validate(missingAnswerModel);

        Assert.Contains(missingAnswerErrors, error => error.Contains("Tacan odgovor je obavezan", StringComparison.OrdinalIgnoreCase));

        var tooLongAnswerModel = new QuestionEditorModel
        {
            Type = "open_answer",
            Text = "Pitanje",
            CategoryId = 1,
            SubtopicId = 1,
            CorrectAnswer = new string('A', QuestionEditorFieldLimits.CorrectAnswerMaxLength + 1)
        };

        var tooLongAnswerErrors = QuestionEditorValidation.Validate(tooLongAnswerModel);

        Assert.Contains(tooLongAnswerErrors, error => error.Contains("Tacan odgovor ne moze imati vise od", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_StepsRequireTextAndSequentialOrder()
    {
        var model = new QuestionEditorModel
        {
            Type = "multiple_choice",
            Text = "Pitanje",
            CategoryId = 1,
            SubtopicId = 1,
            Options =
            [
                new() { Text = "Opcija A", IsCorrect = true },
                new() { Text = "Opcija B", IsCorrect = false }
            ],
            Steps =
            [
                new() { Order = 1, Text = "Prvi korak" },
                new() { Order = 3, Text = string.Empty }
            ]
        };

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Contains(errors, error => error == "Korak 2 mora imati tekst ili ga uklonite.");
        Assert.Contains(errors, error => error.Contains("Redosled koraka mora biti sekvencijalan", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsBlankLatexDelimiters()
    {
        var model = new QuestionEditorModel
        {
            Type = "multiple_choice",
            Text = "Resi $ $",
            CategoryId = 1,
            SubtopicId = 1,
            Options =
            [
                new() { Text = "A", IsCorrect = true },
                new() { Text = "$$", IsCorrect = false }
            ]
        };

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Contains(errors, error => error.Contains("Tekst pitanja: prazna LaTeX formula", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Opcija 2: prazna LaTeX formula", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateDetection_IgnoresEmptyOptions()
    {
        var options = new List<QuestionOptionEditorModel>
        {
            new() { Text = "A" },
            new() { Text = "" },
            new() { Text = "" }
        };

        Assert.False(QuestionEditorValidation.HasDuplicateOptionTexts(options));
    }

    [Fact]
    public void DuplicateDetection_IsCaseInsensitiveAndTrimmed()
    {
        var options = new List<QuestionOptionEditorModel>
        {
            new() { Text = "  Tacno   resenje  " },
            new() { Text = "tacno resenje" }
        };

        Assert.True(QuestionEditorValidation.HasDuplicateOptionTexts(options));
    }

    [Fact]
    public void Validate_ValidMultipleChoice_ReturnsNoErrors()
    {
        var model = new QuestionEditorModel
        {
            Type = "multiple_choice",
            Text = "Sta je 2+2?",
            CategoryId = 1,
            SubtopicId = 1,
            Options =
            [
                new() { Text = "3", IsCorrect = false },
                new() { Text = "4", IsCorrect = true }
            ]
        };

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Empty(errors);
    }

    [Fact]
    public void ApiDbContext_MapsXminAsConcurrencyToken()
    {
        using var db = TestDbContextFactory.Create();

        var xminProperty = db.Model.FindEntityType(typeof(Question))!.FindProperty("xmin");

        Assert.NotNull(xminProperty);
        Assert.True(xminProperty.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, xminProperty.ValueGenerated);
    }

    [Fact]
    public void AdminDbContext_MapsXminAsConcurrencyToken()
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new AdminDbContext(options);
        var xminProperty = db.Model.FindEntityType(typeof(Question))!.FindProperty("xmin");

        Assert.NotNull(xminProperty);
        Assert.True(xminProperty.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, xminProperty.ValueGenerated);
    }
}

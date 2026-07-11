using MathLearning.Admin.Models;

namespace MathLearning.Tests.Services;

public sealed class AdminQuestionValidationAdditionalTests
{
    [Fact]
    public void Validate_NullModel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => QuestionEditorValidation.Validate(null!));
    }

    [Fact]
    public void Validate_WhitespaceQuestionText_ReportsRequiredMessage()
    {
        var model = ValidOpenAnswer();
        model.Text = "   ";

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Contains("Tekst pitanja je obavezan.", errors);
    }

    [Fact]
    public void Validate_UnknownQuestionType_ReportsTypeSelectionError()
    {
        var model = ValidOpenAnswer();
        model.Type = "unsupported";

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Contains("Izaberite tip pitanja.", errors);
    }

    [Fact]
    public void Validate_MultipleChoiceWithOneFilledAndOneBlankOption_ReportsBothShapeErrors()
    {
        var model = ValidMultipleChoice();
        model.Options =
        [
            new() { Text = "Four", IsCorrect = true },
            new() { Text = "   ", IsCorrect = false }
        ];

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Contains("Multiple choice pitanje mora imati najmanje dve opcije.", errors);
        Assert.Contains("Sve opcije moraju biti popunjene ili uklonjene.", errors);
    }

    [Fact]
    public void Validate_HintOverLimit_ReportsHintLengthError()
    {
        var model = ValidOpenAnswer();
        model.HintClue = new string('H', QuestionEditorFieldLimits.HintTextMaxLength + 1);

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Contains(
            $"Hint ne moze imati vise od {QuestionEditorFieldLimits.HintTextMaxLength} karaktera.",
            errors);
    }

    [Fact]
    public void Validate_StepHintOverLimit_ReportsStepHintLengthError()
    {
        var model = ValidMultipleChoice();
        model.Steps =
        [
            new()
            {
                Order = 1,
                Text = "First step",
                Hint = new string('H', QuestionEditorFieldLimits.StepHintMaxLength + 1)
            }
        ];

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Contains(
            $"Napomena u koraku 1 ne moze imati vise od {QuestionEditorFieldLimits.StepHintMaxLength} karaktera.",
            errors);
    }

    [Fact]
    public void Validate_ValidOpenAnswer_ReturnsNoErrors()
    {
        var errors = QuestionEditorValidation.Validate(ValidOpenAnswer());

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ExactMaximumLengths_AreAccepted()
    {
        var model = new QuestionEditorModel
        {
            Type = "open_answer",
            Text = new string('Q', QuestionEditorFieldLimits.QuestionTextMaxLength),
            Explanation = new string('E', QuestionEditorFieldLimits.ExplanationMaxLength),
            HintFormula = new string('F', QuestionEditorFieldLimits.HintTextMaxLength),
            HintClue = new string('C', QuestionEditorFieldLimits.HintTextMaxLength),
            HintFull = new string('H', QuestionEditorFieldLimits.HintTextMaxLength),
            CorrectAnswer = new string('A', QuestionEditorFieldLimits.CorrectAnswerMaxLength),
            CategoryId = 1,
            SubtopicId = 1,
            Steps =
            [
                new()
                {
                    Order = 1,
                    Text = new string('S', QuestionEditorFieldLimits.StepTextMaxLength),
                    Hint = new string('N', QuestionEditorFieldLimits.StepHintMaxLength)
                }
            ]
        };

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_EmptyLatexAcrossFields_ReportsEachAffectedField()
    {
        var model = ValidMultipleChoice();
        model.Explanation = "Explanation with $ $";
        model.HintFormula = "$$ $$";
        model.HintClue = "Use $ $";
        model.HintFull = "Solution $$ $$";
        model.Steps =
        [
            new()
            {
                Order = 1,
                Text = "Step $ $",
                Hint = "Hint $$ $$"
            }
        ];

        var errors = QuestionEditorValidation.Validate(model);

        Assert.Contains(errors, error => error.StartsWith("Objasnjenje:", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.StartsWith("Lagani hint:", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.StartsWith("Naputak:", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.StartsWith("Potpuno resenje:", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.StartsWith("Korak 1:", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.StartsWith("Napomena u koraku 1:", StringComparison.Ordinal));
    }

    [Fact]
    public void NormalizeOptionText_TrimsCollapsesWhitespaceAndLowercases()
    {
        var normalized = QuestionEditorValidation.NormalizeOptionText("  TACNO\t\n  Resenje   ");

        Assert.Equal("tacno resenje", normalized);
    }

    [Fact]
    public void HasDuplicateOptionTexts_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            QuestionEditorValidation.HasDuplicateOptionTexts(null!));
    }

    [Fact]
    public void HasDuplicateOptionTexts_DistinctNormalizedValues_ReturnsFalse()
    {
        var options = new List<QuestionOptionEditorModel>
        {
            new() { Text = "First answer" },
            new() { Text = "Second answer" },
            new() { Text = "   " }
        };

        Assert.False(QuestionEditorValidation.HasDuplicateOptionTexts(options));
    }

    private static QuestionEditorModel ValidOpenAnswer() =>
        new()
        {
            Type = "open_answer",
            Text = "What is two plus two?",
            CorrectAnswer = "4",
            CategoryId = 1,
            SubtopicId = 1
        };

    private static QuestionEditorModel ValidMultipleChoice() =>
        new()
        {
            Type = "multiple_choice",
            Text = "What is two plus two?",
            CategoryId = 1,
            SubtopicId = 1,
            Options =
            [
                new() { Text = "3", IsCorrect = false },
                new() { Text = "4", IsCorrect = true }
            ]
        };
}

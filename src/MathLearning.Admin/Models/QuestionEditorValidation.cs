namespace MathLearning.Admin.Models;

public static class QuestionEditorFieldLimits
{
    public const int MinQuestionTextLength = 4;
    public const int QuestionTextMaxLength = 4000;
    public const int ExplanationMaxLength = 3000;
    public const int CorrectAnswerMaxLength = 2000;
    public const int OptionTextMaxLength = 1000;
    public const int StepTextMaxLength = 2000;
}

public static class QuestionEditorValidation
{
    public static List<string> Validate(QuestionEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var errors = new List<string>();
        var trimmedQuestionText = model.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(model.Text))
        {
            errors.Add("Tekst pitanja je obavezan.");
        }
        else if (trimmedQuestionText.Length < QuestionEditorFieldLimits.MinQuestionTextLength)
        {
            errors.Add($"Tekst pitanja mora imati najmanje {QuestionEditorFieldLimits.MinQuestionTextLength} karaktera.");
        }

        if ((model.Text?.Length ?? 0) > QuestionEditorFieldLimits.QuestionTextMaxLength)
        {
            errors.Add($"Tekst pitanja ne može imati više od {QuestionEditorFieldLimits.QuestionTextMaxLength} karaktera.");
        }

        if ((model.Explanation?.Length ?? 0) > QuestionEditorFieldLimits.ExplanationMaxLength)
        {
            errors.Add($"Objašnjenje ne može imati više od {QuestionEditorFieldLimits.ExplanationMaxLength} karaktera.");
        }

        if (model.CategoryId <= 0)
        {
            errors.Add("Izaberite kategoriju.");
        }

        if (model.SubtopicId <= 0)
        {
            errors.Add("Izaberite podtemu.");
        }

        if (string.Equals(model.Type, "multiple_choice", StringComparison.OrdinalIgnoreCase))
        {
            ValidateMultipleChoice(model, errors);
        }
        else if (string.IsNullOrWhiteSpace(model.CorrectAnswer))
        {
            errors.Add("Tačan odgovor je obavezan za open answer pitanje.");
        }
        else if (model.CorrectAnswer.Length > QuestionEditorFieldLimits.CorrectAnswerMaxLength)
        {
            errors.Add($"Tačan odgovor ne može imati više od {QuestionEditorFieldLimits.CorrectAnswerMaxLength} karaktera.");
        }

        ValidateOptionLengths(model, errors);
        ValidateStepLengths(model, errors);

        return errors;
    }

    public static bool HasDuplicateOptionTexts(IReadOnlyList<QuestionOptionEditorModel> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var normalizedOptions = options
            .Where(option => !string.IsNullOrWhiteSpace(option.Text))
            .Select(option => option.Text.Trim().ToLowerInvariant())
            .ToList();

        return normalizedOptions.Count != normalizedOptions.Distinct().Count();
    }

    private static void ValidateMultipleChoice(QuestionEditorModel model, List<string> errors)
    {
        var filledOptions = model.Options
            .Where(option => !string.IsNullOrWhiteSpace(option.Text))
            .ToList();

        if (filledOptions.Count < 2)
        {
            errors.Add("Multiple choice pitanje mora imati najmanje dve opcije.");
        }

        if (model.Options.Any(option => string.IsNullOrWhiteSpace(option.Text)))
        {
            errors.Add("Sve opcije moraju biti popunjene ili uklonjene.");
        }

        var correctFilledOptions = filledOptions.Count(option => option.IsCorrect);
        if (correctFilledOptions == 0)
        {
            errors.Add("Označite tačan odgovor.");
        }
        else if (correctFilledOptions > 1)
        {
            errors.Add("Multiple choice pitanje može imati samo jedan tačan odgovor.");
        }

        if (HasDuplicateOptionTexts(model.Options))
        {
            errors.Add("Opcije ne smeju imati identičan tekst.");
        }
    }

    private static void ValidateOptionLengths(QuestionEditorModel model, List<string> errors)
    {
        if (model.Options.Any(option => (option.Text?.Length ?? 0) > QuestionEditorFieldLimits.OptionTextMaxLength))
        {
            errors.Add($"Tekst opcije ne može imati više od {QuestionEditorFieldLimits.OptionTextMaxLength} karaktera.");
        }
    }

    private static void ValidateStepLengths(QuestionEditorModel model, List<string> errors)
    {
        if (model.Steps.Any(step => (step.Text?.Length ?? 0) > QuestionEditorFieldLimits.StepTextMaxLength))
        {
            errors.Add($"Tekst koraka ne može imati više od {QuestionEditorFieldLimits.StepTextMaxLength} karaktera.");
        }
    }
}
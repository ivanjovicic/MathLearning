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
            errors.Add($"Tekst pitanja ne moze imati vise od {QuestionEditorFieldLimits.QuestionTextMaxLength} karaktera.");
        }

        if ((model.Explanation?.Length ?? 0) > QuestionEditorFieldLimits.ExplanationMaxLength)
        {
            errors.Add($"Objasnjenje ne moze imati vise od {QuestionEditorFieldLimits.ExplanationMaxLength} karaktera.");
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
        else if (string.Equals(model.Type, "open_answer", StringComparison.OrdinalIgnoreCase))
        {
            ValidateOpenAnswer(model, errors);
        }
        else
        {
            errors.Add("Izaberite tip pitanja.");
        }

        ValidateOptionLengths(model, errors);
        ValidateSteps(model, errors);

        return errors;
    }

    public static bool HasDuplicateOptionTexts(IReadOnlyList<QuestionOptionEditorModel> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var normalizedOptions = options
            .Where(option => !string.IsNullOrWhiteSpace(option.Text))
            .Select(option => NormalizeOptionText(option.Text))
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

        var correctOptions = model.Options.Where(option => option.IsCorrect).ToList();
        if (correctOptions.Count == 0)
        {
            errors.Add("Oznacite tacan odgovor.");
        }
        else if (correctOptions.Count > 1)
        {
            errors.Add("Multiple choice pitanje moze imati samo jedan tacan odgovor.");
        }

        if (correctOptions.Any(option => string.IsNullOrWhiteSpace(option.Text)))
        {
            errors.Add("Oznaceni tacan odgovor mora biti popunjena opcija.");
        }

        if (HasDuplicateOptionTexts(model.Options))
        {
            errors.Add("Opcije ne smeju imati identican tekst.");
        }
    }

    private static void ValidateOpenAnswer(QuestionEditorModel model, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(model.CorrectAnswer))
        {
            errors.Add("Tacan odgovor je obavezan za open answer pitanje.");
        }
        else if (model.CorrectAnswer.Length > QuestionEditorFieldLimits.CorrectAnswerMaxLength)
        {
            errors.Add($"Tacan odgovor ne moze imati vise od {QuestionEditorFieldLimits.CorrectAnswerMaxLength} karaktera.");
        }
    }

    private static void ValidateOptionLengths(QuestionEditorModel model, List<string> errors)
    {
        if (model.Options.Any(option => (option.Text?.Length ?? 0) > QuestionEditorFieldLimits.OptionTextMaxLength))
        {
            errors.Add($"Tekst opcije ne moze imati vise od {QuestionEditorFieldLimits.OptionTextMaxLength} karaktera.");
        }
    }

    private static void ValidateSteps(QuestionEditorModel model, List<string> errors)
    {
        for (var i = 0; i < model.Steps.Count; i++)
        {
            var step = model.Steps[i];
            if (string.IsNullOrWhiteSpace(step.Text))
            {
                errors.Add($"Korak {i + 1} mora imati tekst ili ga uklonite.");
            }

            if ((step.Text?.Length ?? 0) > QuestionEditorFieldLimits.StepTextMaxLength)
            {
                errors.Add($"Tekst koraka {i + 1} ne moze imati vise od {QuestionEditorFieldLimits.StepTextMaxLength} karaktera.");
            }
        }

        if (model.Steps.Count == 0)
        {
            return;
        }

        var orderedStepNumbers = model.Steps
            .Select(step => step.Order)
            .Order()
            .ToArray();

        for (var i = 0; i < orderedStepNumbers.Length; i++)
        {
            if (orderedStepNumbers[i] != i + 1)
            {
                errors.Add("Redosled koraka mora biti sekvencijalan (1..N).");
                break;
            }
        }
    }

    private static string NormalizeOptionText(string value)
        => string.Join(
            ' ',
            value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
}

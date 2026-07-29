using System.Globalization;
using FluentValidation;
using MathLearning.Application.DTOs.Explanations;
using MathLearning.Domain.Explanations;

namespace MathLearning.Application.Validators;

public sealed class GenerateExplanationRequestValidator : AbstractValidator<GenerateExplanationRequest>
{
    public GenerateExplanationRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.ProblemId.HasValue || !string.IsNullOrWhiteSpace(x.ProblemText))
            .WithMessage("Either problemId or problemText is required.");

        RuleFor(x => x.ProblemId)
            .GreaterThan(0)
            .When(x => x.ProblemId.HasValue);

        RuleFor(x => x.ProblemText)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.ProblemText));

        RuleFor(x => x.StudentAnswer)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.StudentAnswer));

        RuleFor(x => x.ExpectedAnswer)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.ExpectedAnswer));

        RuleFor(x => x.Topic)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Topic));

        RuleFor(x => x.Subtopic)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Subtopic));

        RuleFor(x => x.Difficulty)
            .NotEmpty()
            .MaximumLength(20)
            .Must(BeSupportedDifficulty)
            .WithMessage("Difficulty must be one of easy, medium, hard, or advanced.");

        RuleFor(x => x.Language)
            .NotEmpty()
            .MaximumLength(10)
            .Must(BeValidCultureName)
            .WithMessage("Language must be a valid culture name.");

        RuleFor(x => x.Grade)
            .GreaterThanOrEqualTo(0);
    }

    private static bool BeSupportedDifficulty(string difficulty)
    {
        if (!Enum.TryParse<DifficultyLevel>(difficulty, true, out var parsed))
            return false;

        return string.Equals(parsed.ToString(), difficulty.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool BeValidCultureName(string language)
    {
        try
        {
            _ = CultureInfo.GetCultureInfo(language.Trim());
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}

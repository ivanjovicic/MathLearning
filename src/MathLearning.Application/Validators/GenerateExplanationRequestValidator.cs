using FluentValidation;
using MathLearning.Application.DTOs.Explanations;

namespace MathLearning.Application.Validators;

public sealed class GenerateExplanationRequestValidator : AbstractValidator<GenerateExplanationRequest>
{
    public GenerateExplanationRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.ProblemId.HasValue || !string.IsNullOrWhiteSpace(x.ProblemText))
            .WithMessage("Either problemId or problemText is required.");

        RuleFor(x => x.ProblemText)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.ProblemText));

        RuleFor(x => x.Topic)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Topic));

        RuleFor(x => x.Subtopic)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Subtopic));

        RuleFor(x => x.Difficulty)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.Language)
            .NotEmpty()
            .MaximumLength(10);
    }
}

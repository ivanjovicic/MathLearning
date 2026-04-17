using FluentValidation;
using MathLearning.Application.DTOs.Questions;

namespace MathLearning.Application.Validators;

public sealed class QuestionAuthoringRequestValidator : AbstractValidator<QuestionAuthoringRequest>
{
    private const int CorrectAnswerMaxLength = 2000;

    public QuestionAuthoringRequestValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(x => x.Type)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Difficulty)
            .InclusiveBetween(1, 5);

        RuleFor(x => x.CategoryId)
            .GreaterThan(0);

        RuleFor(x => x.SubtopicId)
            .GreaterThan(0);

        RuleFor(x => x.Options)
            .NotNull();

        RuleFor(x => x)
            .Custom(ValidateQuestionTypeRules);

        RuleFor(x => x.SemanticsAltText)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.SemanticsAltText));

        RuleForEach(x => x.Options)
            .SetValidator(new QuestionAuthoringOptionDtoValidator());

        RuleForEach(x => x.Hints)
            .SetValidator(new QuestionHintDtoValidator());

        RuleForEach(x => x.Steps)
            .SetValidator(new StepExplanationAuthoringDtoValidator());

        RuleFor(x => x.Steps)
            .Custom(ValidateStepOrder);
    }

    private static void ValidateQuestionTypeRules(QuestionAuthoringRequest request, ValidationContext<QuestionAuthoringRequest> context)
    {
        if (string.Equals(request.Type, "multiple_choice", StringComparison.OrdinalIgnoreCase))
        {
            ValidateMultipleChoiceRules(request, context);
            return;
        }

        if (string.Equals(request.Type, "open_answer", StringComparison.OrdinalIgnoreCase))
        {
            ValidateOpenAnswerRules(request, context);
        }
    }

    private static void ValidateMultipleChoiceRules(QuestionAuthoringRequest request, ValidationContext<QuestionAuthoringRequest> context)
    {
        if (request.Options is null || request.Options.Count == 0)
        {
            context.AddFailure(nameof(QuestionAuthoringRequest.Options), "Multiple choice question requires at least two options.");
            return;
        }

        if (request.Options.Count < 2)
        {
            context.AddFailure(nameof(QuestionAuthoringRequest.Options), "Multiple choice question requires at least two options.");
        }

        if (request.Options.Any(x => string.IsNullOrWhiteSpace(x.Text)))
        {
            context.AddFailure(nameof(QuestionAuthoringRequest.Options), "Multiple choice options must have non-empty text.");
        }

        var normalizedOptions = request.Options
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .Select(x => x.Text.Trim().ToLowerInvariant())
            .ToArray();

        if (normalizedOptions.Length != normalizedOptions.Distinct().Count())
        {
            context.AddFailure(nameof(QuestionAuthoringRequest.Options), "Multiple choice options must be unique.");
        }

        var correctOptions = request.Options.Where(x => x.IsCorrect).ToArray();
        if (correctOptions.Length == 0)
        {
            context.AddFailure(nameof(QuestionAuthoringRequest.Options), "Multiple choice question must have exactly one correct option.");
        }
        else if (correctOptions.Length > 1)
        {
            context.AddFailure(nameof(QuestionAuthoringRequest.Options), "Multiple choice question cannot have more than one correct option.");
        }

        if (!request.CorrectOptionId.HasValue)
        {
            return;
        }

        var selectedOption = request.Options.FirstOrDefault(x => x.Id == request.CorrectOptionId.Value);
        if (selectedOption is null)
        {
            context.AddFailure(nameof(QuestionAuthoringRequest.CorrectOptionId), "CorrectOptionId must reference an existing option.");
            return;
        }

        if (!selectedOption.IsCorrect)
        {
            context.AddFailure(nameof(QuestionAuthoringRequest.CorrectOptionId), "CorrectOptionId must reference the option marked as correct.");
        }
    }

    private static void ValidateOpenAnswerRules(QuestionAuthoringRequest request, ValidationContext<QuestionAuthoringRequest> context)
    {
        if (string.IsNullOrWhiteSpace(request.CorrectAnswer))
        {
            context.AddFailure(nameof(QuestionAuthoringRequest.CorrectAnswer), "Open answer question requires CorrectAnswer.");
            return;
        }

        var trimmedAnswer = request.CorrectAnswer.Trim();
        if (trimmedAnswer.Length == 0)
        {
            context.AddFailure(nameof(QuestionAuthoringRequest.CorrectAnswer), "Open answer question requires CorrectAnswer.");
        }

        if (trimmedAnswer.Length > CorrectAnswerMaxLength)
        {
            context.AddFailure(nameof(QuestionAuthoringRequest.CorrectAnswer), $"CorrectAnswer cannot exceed {CorrectAnswerMaxLength} characters.");
        }
    }

    private static void ValidateStepOrder(IReadOnlyList<StepExplanationAuthoringDto> steps, ValidationContext<QuestionAuthoringRequest> context)
    {
        if (steps is null || steps.Count == 0)
        {
            return;
        }

        var ordered = steps.OrderBy(x => x.Order).ToArray();
        for (var i = 0; i < ordered.Length; i++)
        {
            var expected = i + 1;
            if (ordered[i].Order != expected)
            {
                context.AddFailure(nameof(QuestionAuthoringRequest.Steps), "Step order must be sequential (1..N).");
                break;
            }
        }
    }
}

public sealed class SaveQuestionDraftRequestValidator : AbstractValidator<SaveQuestionDraftRequest>
{
    public SaveQuestionDraftRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotNull()
            .SetValidator(new QuestionAuthoringRequestValidator()!);

        RuleFor(x => x.ChangeReason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.ChangeReason));
    }
}

public sealed class PublishQuestionRequestValidator : AbstractValidator<PublishQuestionRequest>
{
    public PublishQuestionRequestValidator()
    {
        RuleFor(x => x.DraftId)
            .NotEmpty();

        RuleFor(x => x.ChangeReason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.ChangeReason));
    }
}

internal sealed class QuestionAuthoringOptionDtoValidator : AbstractValidator<QuestionAuthoringOptionDto>
{
    public QuestionAuthoringOptionDtoValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty()
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage("Option text cannot be empty.")
            .MaximumLength(1000);

        RuleFor(x => x.SemanticsAltText)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.SemanticsAltText));
    }
}

internal sealed class QuestionHintDtoValidator : AbstractValidator<QuestionHintDto>
{
    public QuestionHintDtoValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Text)
            .NotEmpty()
            .MaximumLength(2000);

        RuleFor(x => x.SemanticsAltText)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.SemanticsAltText));
    }
}

internal sealed class StepExplanationAuthoringDtoValidator : AbstractValidator<StepExplanationAuthoringDto>
{
    public StepExplanationAuthoringDtoValidator()
    {
        RuleFor(x => x.Order)
            .GreaterThan(0);

        RuleFor(x => x.Text)
            .NotEmpty()
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage("Step text cannot be empty.")
            .MaximumLength(2000);

        RuleFor(x => x.Hint)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.Hint));

        RuleFor(x => x.SemanticsAltText)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.SemanticsAltText));
    }
}

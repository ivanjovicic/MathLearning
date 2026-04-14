using FluentValidation;
using MathLearning.Application.DTOs.Questions;

namespace MathLearning.Application.Validators;

public sealed class QuestionAuthoringRequestValidator : AbstractValidator<QuestionAuthoringRequest>
{
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

        RuleFor(x => x.SemanticsAltText)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.SemanticsAltText));

        RuleForEach(x => x.Options)
            .SetValidator(new QuestionAuthoringOptionDtoValidator());

        RuleForEach(x => x.Hints)
            .SetValidator(new QuestionHintDtoValidator());

        RuleForEach(x => x.Steps)
            .SetValidator(new StepExplanationAuthoringDtoValidator());
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
            .MaximumLength(2000);

        RuleFor(x => x.Hint)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.Hint));

        RuleFor(x => x.SemanticsAltText)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.SemanticsAltText));
    }
}

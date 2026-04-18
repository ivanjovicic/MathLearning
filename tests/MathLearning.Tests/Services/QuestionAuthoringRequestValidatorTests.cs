using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Validators;

namespace MathLearning.Tests.Services;

public class QuestionAuthoringRequestValidatorTests
{
    private readonly QuestionAuthoringRequestValidator validator = new();

    [Fact]
    public void MultipleChoice_RejectsDuplicateOptions()
    {
        var request = CreateValidMultipleChoiceRequest() with
        {
            Options =
            [
                new QuestionAuthoringOptionDto(1, "A", true),
                new QuestionAuthoringOptionDto(2, " a ", false)
            ]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(QuestionAuthoringRequest.Options));
    }

    [Fact]
    public void MultipleChoice_RejectsDuplicateOptions_WhenWhitespaceOnlyDiffers()
    {
        var request = CreateValidMultipleChoiceRequest() with
        {
            Options =
            [
                new QuestionAuthoringOptionDto(1, "Tacan odgovor", true),
                new QuestionAuthoringOptionDto(2, "  tacan   odgovor  ", false)
            ]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(QuestionAuthoringRequest.Options));
    }

    [Fact]
    public void MultipleChoice_RejectsZeroCorrectOption()
    {
        var request = CreateValidMultipleChoiceRequest() with
        {
            Options =
            [
                new QuestionAuthoringOptionDto(1, "A", false),
                new QuestionAuthoringOptionDto(2, "B", false)
            ]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("exactly one correct option", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MultipleChoice_RejectsMultipleCorrectOptions()
    {
        var request = CreateValidMultipleChoiceRequest() with
        {
            Options =
            [
                new QuestionAuthoringOptionDto(1, "A", true),
                new QuestionAuthoringOptionDto(2, "B", true)
            ]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("cannot have more than one correct option", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MultipleChoice_RejectsInvalidCorrectOptionId()
    {
        var request = CreateValidMultipleChoiceRequest() with
        {
            CorrectOptionId = 999
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(QuestionAuthoringRequest.CorrectOptionId));
    }

    [Fact]
    public void MultipleChoice_RejectsCorrectOptionId_WhenOptionIsNotMarkedCorrect()
    {
        var request = CreateValidMultipleChoiceRequest() with
        {
            CorrectOptionId = 2,
            Options =
            [
                new QuestionAuthoringOptionDto(1, "A", true),
                new QuestionAuthoringOptionDto(2, "B", false)
            ]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(QuestionAuthoringRequest.CorrectOptionId));
    }

    [Fact]
    public void MultipleChoice_RejectsEmptyOptionText()
    {
        var request = CreateValidMultipleChoiceRequest() with
        {
            Options =
            [
                new QuestionAuthoringOptionDto(1, "A", true),
                new QuestionAuthoringOptionDto(2, "   ", false)
            ]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(QuestionAuthoringRequest.Options));
    }

    [Fact]
    public void OpenAnswer_RequiresCorrectAnswer()
    {
        var request = CreateValidMultipleChoiceRequest() with
        {
            Type = "open_answer",
            CorrectAnswer = "  ",
            Options = []
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(QuestionAuthoringRequest.CorrectAnswer));
    }

    [Fact]
    public void Question_RejectsTooShortText()
    {
        var request = CreateValidMultipleChoiceRequest() with
        {
            Text = "abc"
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(QuestionAuthoringRequest.Text));
    }

    [Fact]
    public void Steps_MustBeSequential()
    {
        var request = CreateValidMultipleChoiceRequest() with
        {
            Steps =
            [
                new StepExplanationAuthoringDto(1, "First", null, false),
                new StepExplanationAuthoringDto(3, "Third", null, false)
            ]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(QuestionAuthoringRequest.Steps));
    }

    private static QuestionAuthoringRequest CreateValidMultipleChoiceRequest()
        => new(
            QuestionId: null,
            Text: "Koliko je 2 + 2?",
            Type: "multiple_choice",
            CorrectAnswer: "4",
            Explanation: "2 + 2 = 4",
            Difficulty: 1,
            CategoryId: 1,
            SubtopicId: 1,
            Options:
            [
                new QuestionAuthoringOptionDto(1, "4", true),
                new QuestionAuthoringOptionDto(2, "5", false)
            ],
            Hints: [],
            Steps:
            [
                new StepExplanationAuthoringDto(1, "Saberi brojeve", null, false)
            ],
            CorrectOptionId: 1
        );
}

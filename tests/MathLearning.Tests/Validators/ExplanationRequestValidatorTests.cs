using MathLearning.Application.DTOs.Explanations;
using MathLearning.Application.Validators;

namespace MathLearning.Tests.Validators;

public sealed class ExplanationRequestValidatorTests
{
    private readonly GenerateExplanationRequestValidator generateValidator = new();
    private readonly MistakeAnalysisRequestValidator mistakeValidator = new();

    [Fact]
    public void GenerateValidator_AcceptsValidRequest()
    {
        var result = generateValidator.Validate(ValidGenerateRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void GenerateValidator_RejectsInvalidBoundaries()
    {
        var request = ValidGenerateRequest() with
        {
            ProblemId = 0,
            Grade = -1,
            Difficulty = "impossible",
            Language = "not-a-culture",
            Topic = new string('t', 101),
            StudentAnswer = new string('s', 201),
        };

        var result = generateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GenerateExplanationRequest.ProblemId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GenerateExplanationRequest.Grade));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GenerateExplanationRequest.Difficulty));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GenerateExplanationRequest.Language));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GenerateExplanationRequest.Topic));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GenerateExplanationRequest.StudentAnswer));
    }

    [Fact]
    public void MistakeValidator_AcceptsValidRequest()
    {
        var result = mistakeValidator.Validate(ValidMistakeRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void MistakeValidator_RejectsInvalidBoundaries()
    {
        var request = ValidMistakeRequest() with
        {
            ProblemId = -2,
            Grade = -1,
            Difficulty = "unknown",
            Language = "nope",
            ProblemText = new string('p', 1001),
            StudentAnswer = new string('s', 201),
            ExpectedAnswer = new string('e', 201),
            Topic = new string('t', 101),
            Subtopic = new string('u', 101),
        };

        var result = mistakeValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MistakeAnalysisRequest.ProblemId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MistakeAnalysisRequest.Grade));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MistakeAnalysisRequest.Difficulty));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MistakeAnalysisRequest.Language));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MistakeAnalysisRequest.ProblemText));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MistakeAnalysisRequest.StudentAnswer));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MistakeAnalysisRequest.ExpectedAnswer));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MistakeAnalysisRequest.Topic));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(MistakeAnalysisRequest.Subtopic));
    }

    private static GenerateExplanationRequest ValidGenerateRequest() => new(
        ProblemId: 17,
        ProblemText: "2 + 2",
        StudentAnswer: "4",
        ExpectedAnswer: "4",
        Topic: "Arithmetic",
        Subtopic: "Addition",
        Grade: 5,
        Difficulty: "easy",
        Language: "en");

    private static MistakeAnalysisRequest ValidMistakeRequest() => new(
        ProblemId: 17,
        ProblemText: "2 + 2",
        StudentAnswer: "5",
        ExpectedAnswer: "4",
        Topic: "Arithmetic",
        Subtopic: "Addition",
        Grade: 5,
        Difficulty: "easy",
        Language: "en");
}

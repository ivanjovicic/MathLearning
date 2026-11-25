using System.Text.Json.Serialization;

namespace MathLearning.Application.DTOs.Quiz;

public record SubmitAnswerResponse(
    bool IsCorrect,
    string? Explanation,
    List<StepExplanationDto>? Steps = null,
    bool IsFirstTimeCorrect = false,
    int AwardedXp = 0,
    int TotalXp = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IssuedMathVisualDto? MathVisual = null
);

public record IssuedMathVisualDto(
    int SchemaVersion,
    int QuestionId,
    int IssuedQuestionVersion,
    string ReviewedContentHash,
    string TemplateId,
    int TemplateVersion,
    string Locale,
    string Operation,
    string TaskShape,
    int Rows,
    int Columns,
    string VisualAidType,
    int[] Groups,
    IssuedMathVisualStepDto[] ExplanationSteps,
    string RevealStage,
    string AnswerSettlement);

public record IssuedMathVisualStepDto(int Order, string Text);

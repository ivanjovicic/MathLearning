namespace MathLearning.Application.DTOs.Practice;

public sealed record StartPracticeSessionRequest(
    string? UserId,
    string? SkillNodeId,
    int? TopicId,
    int? SubtopicId,
    int? TargetQuestions,
    string? PreferredDifficulty);

public sealed record PracticeQuestionOptionDto(
    int Id,
    string Text);

public sealed record PracticeQuestionDto(
    int Id,
    string Prompt,
    IReadOnlyList<PracticeQuestionOptionDto> Options,
    string Difficulty);

public sealed record StartPracticeSessionResponse(
    Guid SessionId,
    string? SkillNodeId,
    string RecommendedDifficulty,
    decimal InitialMastery,
    PracticeQuestionDto? Question);

public sealed record SubmitPracticeAnswerRequest(
    int QuestionId,
    string SelectedOption,
    int TimeSpentMs);

public sealed record SubmitPracticeAnswerResponse(
    bool IsCorrect,
    string Feedback,
    decimal MasteryBefore,
    decimal MasteryAfter,
    int XpEarned,
    PracticeQuestionDto? NextQuestion);

public sealed record CompletePracticeSessionResponse(
    Guid SessionId,
    string Status,
    int AnsweredQuestions,
    int CorrectAnswers,
    decimal Accuracy,
    int XpEarned,
    decimal InitialMastery,
    decimal FinalMastery,
    decimal MasteryDelta,
    bool WeakTopicsUpdated,
    string? RecommendedNextSkillNodeId);

namespace MathLearning.Application.DTOs.Quiz;

/// <summary>
/// Represents a request to submit an answer for a quiz question.
/// </summary>
public record SubmitAnswerRequest(
    /// <summary>
    /// Gets or sets the unique identifier of the quiz. This can be an empty string.
    /// </summary>
    string QuizId,
    
    /// <summary>
    /// Gets or sets the unique identifier of the question.
    /// </summary>
    int QuestionId,
    
    /// <summary>
    /// Gets or sets the answer provided by the user.
    /// </summary>
    string Answer,
    
    /// <summary>
    /// Gets or sets the time spent on the question, in seconds.
    /// </summary>
    int TimeSpentSeconds
);

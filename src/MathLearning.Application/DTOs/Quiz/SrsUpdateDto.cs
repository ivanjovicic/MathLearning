namespace MathLearning.Application.DTOs.Quiz;

public class SrsUpdateDto
{
    public int QuestionId { get; set; }
    public bool IsCorrect { get; set; }
    public int TimeMs { get; set; }
    public string? OperationId { get; set; }
    public string? IdempotencyKey { get; set; }
}

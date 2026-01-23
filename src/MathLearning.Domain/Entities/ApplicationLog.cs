namespace MathLearning.Domain.Entities;

public class ApplicationLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? Properties { get; set; }
    public string? RequestPath { get; set; }
    public string? UserName { get; set; }
    public string? MachineName { get; set; }
}

namespace MathLearning.Infrastructure.Persistance.Models;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTime OccurredUtc { get; set; }
    public string Type { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public DateTime? ProcessedUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

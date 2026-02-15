namespace MathLearning.Domain.Entities;

public class UserDailyStat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public DateOnly Day { get; set; }
    public bool Completed { get; set; }
}

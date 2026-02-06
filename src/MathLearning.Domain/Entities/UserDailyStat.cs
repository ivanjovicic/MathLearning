namespace MathLearning.Domain.Entities;

public class UserDailyStat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public DateOnly Day { get; set; }
    public bool Completed { get; set; }
}

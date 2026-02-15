namespace MathLearning.Domain.Entities;

public class UserFriend
{
    public string UserId { get; set; } = string.Empty;
    public string FriendId { get; set; } = string.Empty;

    // Navigation properties (opciono za sada, jer nemaš User entitet)
    // public User? User { get; set; }
    // public User? Friend { get; set; }
}

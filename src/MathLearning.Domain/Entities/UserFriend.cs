namespace MathLearning.Domain.Entities;

public class UserFriend
{
    public int UserId { get; set; }
    public int FriendId { get; set; }

    // Navigation properties (opciono za sada, jer nemaš User entitet)
    // public User? User { get; set; }
    // public User? Friend { get; set; }
}

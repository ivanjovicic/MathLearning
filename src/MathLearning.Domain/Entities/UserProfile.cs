namespace MathLearning.Domain.Entities;

public class UserProfile
{
    public int Id { get; set; }
    public int UserId { get; set; } // Maps to AspNetUsers Id (hash)
    public string Username { get; set; } = string.Empty;
    
    // 💰 Coin System
    public int Coins { get; set; } = 100; // Start with 100 coins
    public int TotalCoinsEarned { get; set; } = 0;
    public int TotalCoinsSpent { get; set; } = 0;
    
    // 📊 Stats
    public int Level { get; set; } = 1;
    public int Xp { get; set; } = 0;
    public int Streak { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

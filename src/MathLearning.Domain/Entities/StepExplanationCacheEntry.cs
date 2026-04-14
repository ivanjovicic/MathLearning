namespace MathLearning.Domain.Entities;

public class StepExplanationCacheEntry
{
    public Guid Id { get; private set; }
    public string ProblemHash { get; private set; } = "";
    public int Grade { get; private set; }
    public string Difficulty { get; private set; } = "";
    public string PayloadJson { get; private set; } = "";
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; private set; }
    public DateTime LastAccessedAt { get; private set; } = DateTime.UtcNow;

    private StepExplanationCacheEntry() { }

    public StepExplanationCacheEntry(string problemHash, int grade, string difficulty, string payloadJson, DateTime expiresAt)
    {
        Id = Guid.NewGuid();
        SetProblemHash(problemHash);
        Grade = grade;
        SetDifficulty(difficulty);
        SetPayloadJson(payloadJson);
        ExpiresAt = expiresAt;
        LastAccessedAt = DateTime.UtcNow;
    }

    public void SetProblemHash(string problemHash)
    {
        ProblemHash = string.IsNullOrWhiteSpace(problemHash) ? throw new ArgumentException("Problem hash is required.", nameof(problemHash)) : problemHash.Trim();
    }

    public void SetDifficulty(string difficulty)
    {
        Difficulty = string.IsNullOrWhiteSpace(difficulty) ? throw new ArgumentException("Difficulty is required.", nameof(difficulty)) : difficulty.Trim();
    }

    public void SetPayloadJson(string payloadJson)
    {
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? throw new ArgumentException("Payload is required.", nameof(payloadJson)) : payloadJson.Trim();
        LastAccessedAt = DateTime.UtcNow;
    }

    public void RefreshExpiry(DateTime expiresAt)
    {
        ExpiresAt = expiresAt;
        LastAccessedAt = DateTime.UtcNow;
    }
}

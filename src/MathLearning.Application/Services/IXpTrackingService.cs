using MathLearning.Domain.Entities;

namespace MathLearning.Application.Services;

/// <summary>
/// Write path for XP events. All XP grants must go through this service
/// to ensure immutable audit trail, deduplication, and anti-cheat validation.
/// </summary>
public interface IXpTrackingService
{
    Task<UserProfile> AddXpAsync(
        string userId,
        int xpAmount,
        string sourceType = "manual_adjustment",
        string? sourceId = null,
        string? metadataJson = null,
        CancellationToken ct = default);

    Task ResetTimeBasedXpAsync(string userId, CancellationToken ct = default);
}

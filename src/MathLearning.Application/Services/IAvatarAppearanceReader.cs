using MathLearning.Application.DTOs.Cosmetics;

namespace MathLearning.Application.Services;

public interface IAvatarAppearanceReader
{
    Task<AvatarAppearanceDto?> GetAppearanceAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, AvatarAppearanceDto>> GetAppearancesAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default);
}

public sealed class CosmeticAvatarOwnershipException : Exception
{
    public CosmeticAvatarOwnershipException(string message) : base(message)
    {
    }
}

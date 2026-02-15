using System.Security.Cryptography;
using MathLearning.Domain.Entities;

namespace MathLearning.Infrastructure.Services;

public class RefreshTokenService
{
    /// <summary>
    /// Generiše kriptografski siguran random token (64 bytes = 128 hex chars)
    /// </summary>
    public static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Kreira novi RefreshToken entity
    /// </summary>
    public static RefreshToken CreateRefreshToken(
        string userId,
        string? device = null, 
        string? ipAddress = null,
        int expiryDays = 14)
    {
        return new RefreshToken
        {
            UserId = userId,
            Token = GenerateRefreshToken(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            Device = device,
            IpAddress = ipAddress
        };
    }

    /// <summary>
    /// Validira refresh token
    /// </summary>
    public static bool ValidateRefreshToken(RefreshToken? token)
    {
        if (token == null)
            return false;

        if (token.IsRevoked)
            return false;

        if (token.IsExpired)
            return false;

        return true;
    }

    /// <summary>
    /// Revoke token (mark as revoked)
    /// </summary>
    public static void RevokeToken(RefreshToken token)
    {
        token.RevokedAt = DateTime.UtcNow;
    }
}

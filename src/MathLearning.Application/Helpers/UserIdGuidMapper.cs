using System.Security.Cryptography;
using System.Text;

namespace MathLearning.Application.Helpers;

public static class UserIdGuidMapper
{
    public static Guid FromAppUserId(int userId)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, userId);
        return new Guid(bytes);
    }

    public static Guid FromIdentityUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required.", nameof(userId));

        if (Guid.TryParse(userId, out var parsed))
            return parsed;

        // Stable fallback for non-GUID identity keys.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, guidBytes.Length);
        return new Guid(guidBytes);
    }
}

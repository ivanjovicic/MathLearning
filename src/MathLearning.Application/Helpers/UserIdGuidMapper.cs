namespace MathLearning.Application.Helpers;

public static class UserIdGuidMapper
{
    public static Guid FromAppUserId(int userId)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, userId);
        return new Guid(bytes);
    }
}

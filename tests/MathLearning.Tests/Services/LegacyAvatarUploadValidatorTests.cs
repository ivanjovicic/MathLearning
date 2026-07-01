using MathLearning.Api.Services;

namespace MathLearning.Tests.Services;

public sealed class LegacyAvatarUploadValidatorTests
{
    [Fact]
    public void IsSafeFileName_RejectsPathTraversalAndForeignPrefix()
    {
        Assert.False(LegacyAvatarUploadValidator.IsSafeFileName("1001", "../1001_secret.png"));
        Assert.False(LegacyAvatarUploadValidator.IsSafeFileName("1001", "1002_abc.png"));
        Assert.True(LegacyAvatarUploadValidator.IsSafeFileName("1001", "1001_abc123.png"));
    }
}

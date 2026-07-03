using MathLearning.Application.Helpers;

namespace MathLearning.Tests.Helpers;

public sealed class UserIdGuidMapperTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(int.MaxValue)]
    public void FromAppUserId_IsDeterministicAndNonEmptyForPositiveIds(int userId)
    {
        var first = UserIdGuidMapper.FromAppUserId(userId);
        var second = UserIdGuidMapper.FromAppUserId(userId);

        Assert.Equal(first, second);
        Assert.NotEqual(Guid.Empty, first);
    }

    [Fact]
    public void FromAppUserId_DifferentIdsProduceDifferentGuids()
    {
        var mapped = Enumerable.Range(1, 1_000)
            .Select(UserIdGuidMapper.FromAppUserId)
            .ToArray();

        Assert.Equal(mapped.Length, mapped.Distinct().Count());
    }

    [Fact]
    public void FromIdentityUserId_WhenInputIsGuid_ReturnsTheSameGuid()
    {
        var expected = Guid.Parse("be4bbb5a-1638-427d-a4ce-442551e7613f");

        var actual = UserIdGuidMapper.FromIdentityUserId(expected.ToString("D").ToUpperInvariant());

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FromIdentityUserId_NonGuidValueUsesStableSha256Mapping()
    {
        const string identityUserId = "user-123";
        var expected = Guid.Parse("dfc6defc-444d-c6db-37c7-c5b58efface5");

        var first = UserIdGuidMapper.FromIdentityUserId(identityUserId);
        var second = UserIdGuidMapper.FromIdentityUserId(identityUserId);

        Assert.Equal(expected, first);
        Assert.Equal(first, second);
        Assert.NotEqual(Guid.Empty, first);
    }

    [Fact]
    public void FromIdentityUserId_DifferentValuesRemainIsolated()
    {
        var values = Enumerable.Range(1, 1_000)
            .Select(index => UserIdGuidMapper.FromIdentityUserId($"identity-user-{index}"))
            .ToArray();

        Assert.Equal(values.Length, values.Distinct().Count());
    }

    [Fact]
    public void FromIdentityUserId_IsCaseSensitiveForNonGuidIdentityKeys()
    {
        var lower = UserIdGuidMapper.FromIdentityUserId("identity-user");
        var upper = UserIdGuidMapper.FromIdentityUserId("IDENTITY-USER");

        Assert.NotEqual(lower, upper);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromIdentityUserId_MissingValueThrowsArgumentException(string? userId)
    {
        var error = Assert.Throws<ArgumentException>(() =>
            UserIdGuidMapper.FromIdentityUserId(userId!));

        Assert.Equal("userId", error.ParamName);
        Assert.Contains("required", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}

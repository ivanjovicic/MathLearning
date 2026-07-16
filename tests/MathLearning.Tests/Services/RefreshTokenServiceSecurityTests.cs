using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Services;

public sealed class RefreshTokenServiceSecurityTests
{
    [Fact]
    public void GenerateRefreshToken_ReturnsCryptographicallySizedUniqueValues()
    {
        var first = RefreshTokenService.GenerateRefreshToken();
        var second = RefreshTokenService.GenerateRefreshToken();

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.False(string.IsNullOrWhiteSpace(second));
        Assert.NotEqual(first, second);
        Assert.Equal(64, Convert.FromBase64String(first).Length);
        Assert.Equal(64, Convert.FromBase64String(second).Length);
        Assert.Equal(88, first.Length);
        Assert.Equal(88, second.Length);
    }

    [Fact]
    public void GeneratedRefreshToken_FitsConfiguredEntityMaxLength()
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        using var db = new ApiDbContext(options);

        var tokenProperty = db.Model
            .FindEntityType(typeof(RefreshToken))?
            .FindProperty(nameof(RefreshToken.Token));

        Assert.NotNull(tokenProperty);
        Assert.Equal(128, tokenProperty!.GetMaxLength());

        var token = RefreshTokenService.GenerateRefreshToken();

        Assert.True(token.Length <= tokenProperty.GetMaxLength());
    }

    [Fact]
    public void CreateRefreshToken_PreservesScopeMetadataAndExpiryWindow()
    {
        var before = DateTime.UtcNow;

        var token = RefreshTokenService.CreateRefreshToken(
            "user-1",
            "security-stamp-1",
            device: "android-device",
            ipAddress: "127.0.0.1",
            expiryDays: 7);

        var after = DateTime.UtcNow;

        Assert.Equal("user-1", token.UserId);
        Assert.Equal("security-stamp-1", token.SecurityStamp);
        Assert.Equal("android-device", token.Device);
        Assert.Equal("127.0.0.1", token.IpAddress);
        Assert.False(string.IsNullOrWhiteSpace(token.Token));
        Assert.InRange(token.CreatedAt, before, after);
        Assert.InRange(token.ExpiresAt, before.AddDays(7), after.AddDays(7));
        Assert.Null(token.RevokedAt);
        Assert.True(token.IsActive);
    }

    [Fact]
    public void ValidateRefreshToken_NullToken_ReturnsFalse()
    {
        Assert.False(RefreshTokenService.ValidateRefreshToken(null));
    }

    [Fact]
    public void ValidateRefreshToken_ActiveToken_ReturnsTrue()
    {
        var token = NewToken(expiresAt: DateTime.UtcNow.AddMinutes(5));

        Assert.True(RefreshTokenService.ValidateRefreshToken(token));
    }

    [Fact]
    public void ValidateRefreshToken_ExpiredToken_ReturnsFalse()
    {
        var token = NewToken(expiresAt: DateTime.UtcNow.AddSeconds(-1));

        Assert.False(RefreshTokenService.ValidateRefreshToken(token));
        Assert.True(token.IsExpired);
    }

    [Fact]
    public void ValidateRefreshToken_RevokedToken_ReturnsFalse()
    {
        var token = NewToken(expiresAt: DateTime.UtcNow.AddMinutes(5));
        token.RevokedAt = DateTime.UtcNow;

        Assert.False(RefreshTokenService.ValidateRefreshToken(token));
        Assert.True(token.IsRevoked);
    }

    [Fact]
    public void RevokeToken_IsIdempotentAndPreservesFirstRevocationTimestamp()
    {
        var token = NewToken(expiresAt: DateTime.UtcNow.AddMinutes(5));

        RefreshTokenService.RevokeToken(token);
        var firstRevokedAt = token.RevokedAt;
        RefreshTokenService.RevokeToken(token);

        Assert.NotNull(firstRevokedAt);
        Assert.Equal(firstRevokedAt, token.RevokedAt);
        Assert.False(token.IsActive);
    }

    [Fact]
    public void CreateRefreshToken_WithPastExpiry_IsImmediatelyInvalid()
    {
        var token = RefreshTokenService.CreateRefreshToken(
            "user-1",
            "security-stamp-1",
            expiryDays: -1);

        Assert.True(token.IsExpired);
        Assert.False(RefreshTokenService.ValidateRefreshToken(token));
    }

    private static RefreshToken NewToken(DateTime expiresAt)
        => new()
        {
            UserId = "user-1",
            Token = "refresh-token",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt = expiresAt
        };
}

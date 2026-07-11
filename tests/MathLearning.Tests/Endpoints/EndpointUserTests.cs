using System.Security.Claims;
using MathLearning.Api.Endpoints;
using Microsoft.AspNetCore.Http;

namespace MathLearning.Tests.Endpoints;

public sealed class EndpointUserTests
{
    [Fact]
    public void GetUserId_ReturnsUserIdClaim()
    {
        var context = CreateContext(
            new Claim("userId", "user-123"),
            new Claim(ClaimTypes.Name, "Ivan"));

        var userId = EndpointUser.GetUserId(context);

        Assert.Equal("user-123", userId);
    }

    [Fact]
    public void GetUserId_WithMultipleUserIdClaims_ReturnsFirstClaim()
    {
        var context = CreateContext(
            new Claim("userId", "first-user"),
            new Claim("userId", "second-user"));

        var userId = EndpointUser.GetUserId(context);

        Assert.Equal("first-user", userId);
    }

    [Fact]
    public void GetUserId_WithoutUserIdClaim_ReturnsNull()
    {
        var context = CreateContext(new Claim(ClaimTypes.NameIdentifier, "identity-user"));

        var userId = EndpointUser.GetUserId(context);

        Assert.Null(userId);
    }

    [Fact]
    public void GetUserId_WithEmptyUserIdClaim_PreservesEmptyValue()
    {
        var context = CreateContext(new Claim("userId", string.Empty));

        var userId = EndpointUser.GetUserId(context);

        Assert.Equal(string.Empty, userId);
    }

    private static DefaultHttpContext CreateContext(params Claim[] claims)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"))
        };

        return context;
    }
}

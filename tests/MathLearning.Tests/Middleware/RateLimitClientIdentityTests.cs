using System.Net;
using System.Security.Claims;
using MathLearning.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace MathLearning.Tests.Middleware;

public sealed class RateLimitClientIdentityTests
{
    [Fact]
    public void Resolve_AuthenticatedUser_UsesUserIdClaim()
    {
        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim("userId", "learner-42")],
                authenticationType: "Test"));

        Assert.Equal("user:learner-42", RateLimitClientIdentity.Resolve(context));
    }

    [Fact]
    public void Resolve_Anonymous_UsesPhysicalPeerIpFromItems()
    {
        var context = new DefaultHttpContext
        {
            Connection = { RemoteIpAddress = IPAddress.Parse("203.0.113.9") }
        };
        context.Items[ConnectionRemoteIpMiddleware.ItemKey] = IPAddress.Parse("198.51.100.4");

        Assert.Equal("ip:198.51.100.4", RateLimitClientIdentity.Resolve(context));
    }

    [Fact]
    public void Resolve_Anonymous_IgnoresSpoofedRemoteIpAfterForwardedHeaders()
    {
        var context = new DefaultHttpContext
        {
            Connection = { RemoteIpAddress = IPAddress.Parse("1.1.1.1") }
        };
        context.Items[ConnectionRemoteIpMiddleware.ItemKey] = IPAddress.Parse("198.51.100.4");

        Assert.Equal("ip:198.51.100.4", RateLimitClientIdentity.Resolve(context));
        Assert.NotEqual("ip:1.1.1.1", RateLimitClientIdentity.Resolve(context));
    }
}

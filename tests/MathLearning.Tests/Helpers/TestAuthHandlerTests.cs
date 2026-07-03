using System.Security.Claims;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Helpers;

public sealed class TestAuthHandlerTests
{
    [Fact]
    public async Task NoHeaders_UsesCompatibilityDefaultAuthenticatedUser()
    {
        await using var provider = BuildProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };

        var result = await provider
            .GetRequiredService<IAuthenticationService>()
            .AuthenticateAsync(context, "Test");

        Assert.True(result.Succeeded);
        Assert.Equal("test-user", result.Principal?.FindFirst("userId")?.Value);
        Assert.True(result.Principal?.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task ExplicitAnonymousHeader_ReturnsNoAuthenticationResult()
    {
        await using var provider = BuildProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };
        context.Request.Headers[TestAuthHandler.AnonymousHeader] = "true";

        var result = await provider
            .GetRequiredService<IAuthenticationService>()
            .AuthenticateAsync(context, "Test");

        Assert.False(result.Succeeded);
        Assert.True(result.None);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task ExplicitUserAndRoles_CreateExpectedClaims()
    {
        await using var provider = BuildProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };
        context.Request.Headers["X-Test-UserId"] = "  learner-42  ";
        context.Request.Headers["X-Test-Roles"] = "UiTokensAdmin, ContentAuthor";

        var result = await provider
            .GetRequiredService<IAuthenticationService>()
            .AuthenticateAsync(context, "Test");

        Assert.True(result.Succeeded);
        Assert.Equal("learner-42", result.Principal?.FindFirst("userId")?.Value);
        Assert.True(result.Principal?.IsInRole("UiTokensAdmin"));
        Assert.True(result.Principal?.IsInRole("ContentAuthor"));
        Assert.Equal(
            new[] { "UiTokensAdmin", "ContentAuthor" },
            result.Principal?
                .FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .ToArray());
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        return services.BuildServiceProvider();
    }
}

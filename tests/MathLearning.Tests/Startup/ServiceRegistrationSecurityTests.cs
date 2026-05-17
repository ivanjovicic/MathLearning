using MathLearning.Api;
using MathLearning.Api.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace MathLearning.Tests.Startup;

public sealed class ServiceRegistrationSecurityTests
{
    private const string FallbackJwtSecret = "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";

    [Fact]
    public void AddCorsAndSwagger_NonDevWithoutAllowedOrigins_Throws()
    {
        var builder = CreateBuilder("Production");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddCorsAndSwagger());

        Assert.Equal("Cors:AllowedOrigins must be configured outside Development/Test.", exception.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    public void AddCorsAndSwagger_DevelopmentOrTestWithoutAllowedOrigins_DoesNotThrow(string environmentName)
    {
        var builder = CreateBuilder(environmentName);

        var exception = Record.Exception(() => builder.AddCorsAndSwagger());

        Assert.Null(exception);
    }

    [Fact]
    public void AddSecurityServices_NonDevWithoutJwtSecret_Throws()
    {
        var builder = CreateBuilder("Production");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddSecurityServices());

        Assert.Equal("JwtSettings:SecretKey must be configured outside Development/Test.", exception.Message);
    }

    [Fact]
    public void AddSecurityServices_NonDevWithFallbackJwtSecret_Throws()
    {
        var builder = CreateBuilder("Production", new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = FallbackJwtSecret
        });

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddSecurityServices());

        Assert.Equal("JwtSettings:SecretKey must be configured outside Development/Test.", exception.Message);
    }

    [Fact]
    public void AddSecurityServices_NonDevWithShortJwtSecret_Throws()
    {
        var builder = CreateBuilder("Production", new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "short-secret"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddSecurityServices());

        Assert.Equal("JwtSettings:SecretKey must be at least 32 characters.", exception.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    public void AddSecurityServices_DevelopmentOrTestWithoutJwtSecret_UsesFallback(string environmentName)
    {
        var builder = CreateBuilder(environmentName);

        var exception = Record.Exception(() => builder.AddSecurityServices());

        Assert.Null(exception);
    }

    private static WebApplicationBuilder CreateBuilder(
        string environmentName,
        Dictionary<string, string?>? configuration = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName,
            ApplicationName = typeof(Program).Assembly.FullName
        });

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(configuration ?? new Dictionary<string, string?>());

        return builder;
    }
}

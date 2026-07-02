using MathLearning.Api.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace MathLearning.Tests.Startup;

public sealed class SeedAdminStartupPolicyTests
{
    [Fact]
    public void Development_UsesDefaultPasswordAndResetsOnStart()
    {
        var environment = new TestHostEnvironment("Development");
        var configuration = new ConfigurationBuilder().Build();

        var policy = SeedAdminStartupPolicy.Evaluate(environment, configuration);

        Assert.True(policy.ShouldSeed);
        Assert.True(policy.ResetPasswordOnStart);
        Assert.Equal(SeedAdminStartupPolicy.DevelopmentDefaultPassword, policy.Password);
        Assert.False(policy.SuppressedPasswordReset);
    }

    [Fact]
    public void Production_WithoutPassword_SkipsSeeding()
    {
        var environment = new TestHostEnvironment("Production");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedAdmin:Enabled"] = "true"
            })
            .Build();

        var policy = SeedAdminStartupPolicy.Evaluate(environment, configuration);

        Assert.False(policy.ShouldSeed);
        Assert.Contains("Password", policy.SkipReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_WithDevelopmentDefaultPassword_SkipsSeeding()
    {
        var environment = new TestHostEnvironment("Production");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedAdmin:Enabled"] = "true",
                ["SeedAdmin:Password"] = SeedAdminStartupPolicy.DevelopmentDefaultPassword
            })
            .Build();

        var policy = SeedAdminStartupPolicy.Evaluate(environment, configuration);

        Assert.False(policy.ShouldSeed);
        Assert.Contains("default password", policy.SkipReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_ResetPasswordOnStart_RequiresEmergencyFlag()
    {
        var environment = new TestHostEnvironment("Production");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedAdmin:Enabled"] = "true",
                ["SeedAdmin:Password"] = "ProductionOnly!Secret123",
                ["SeedAdmin:ResetPasswordOnStart"] = "true"
            })
            .Build();

        var policy = SeedAdminStartupPolicy.Evaluate(environment, configuration);

        Assert.True(policy.ShouldSeed);
        Assert.False(policy.ResetPasswordOnStart);
        Assert.True(policy.SuppressedPasswordReset);
    }

    [Fact]
    public void Production_EmergencyFlag_AllowsResetOnStart()
    {
        var environment = new TestHostEnvironment("Production");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedAdmin:Enabled"] = "true",
                ["SeedAdmin:Password"] = "ProductionOnly!Secret123",
                ["SeedAdmin:ResetPasswordOnStart"] = "true",
                ["SeedAdmin:AllowEmergencyPasswordReset"] = "true"
            })
            .Build();

        var policy = SeedAdminStartupPolicy.Evaluate(environment, configuration);

        Assert.True(policy.ShouldSeed);
        Assert.True(policy.ResetPasswordOnStart);
        Assert.False(policy.SuppressedPasswordReset);
    }

    [Fact]
    public void Production_Disabled_SkipsSeeding()
    {
        var environment = new TestHostEnvironment("Production");
        var configuration = new ConfigurationBuilder().Build();

        var policy = SeedAdminStartupPolicy.Evaluate(environment, configuration);

        Assert.False(policy.ShouldSeed);
        Assert.Equal("SeedAdmin is disabled.", policy.SkipReason);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "MathLearning.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

using MathLearning.Admin.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MathLearning.Tests.Helpers;

public sealed class AdminWebApplicationFactory : WebApplicationFactory<AdminDbContext>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var dataProtectionKeysPath = Path.Combine(Path.GetTempPath(), $"mathlearning-admin-test-keys-{Guid.NewGuid():N}");

            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AdminIdentity"] = "Host=localhost;Port=5432;Database=mathlearning_admin_tests;Username=test;Password=test",
                ["Database:InitializeOnStartup"] = "false",
                ["SeedAdmin:Enabled"] = "false",
                ["DataProtection:KeysPath"] = dataProtectionKeysPath
            });
        });
    }
}

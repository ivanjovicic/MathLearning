using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;
using MathLearning.Core.Services;
using MathLearning.Core.DTOs;
using System.Linq;
using System.Reflection;

namespace MathLearning.Tests.Helpers;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
            // Ensure the process environment is `Test` so Program.cs detects it early (migrations/registrations can change).
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
            // Also set the IWebHostBuilder environment for completeness.
            builder.UseEnvironment("Test");

            builder.ConfigureTestServices(services =>
            {
                // Customize services for testing if needed
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

                // Ensure any existing LeaderboardService registrations are removed so our mock is used
                services.RemoveAll<MathLearning.Application.Services.ILeaderboardService>();

                // Remove only the hosted services that perform DB activity during startup.
                // Use keyword-based detection to catch both direct ImplementationType registrations
                // and factory-based registrations (e.g., sp => (WeaknessAnalysisScheduler)sp.GetRequiredService<IWeaknessAnalysisScheduler>()).
                var removalKeywords = new[] { "IndexMaintenance", "XpReset", "WeaknessAnalysis", "WeaknessAnalysisScheduler", "WeaknessAnalysisDailyHostedService" };
                var hosted = services.Where(sd => sd.ServiceType == typeof(IHostedService)).ToList();
                foreach (var sd in hosted)
                {
                    var implTypeName = sd.ImplementationType?.Name;
                    var instTypeName = sd.ImplementationInstance?.GetType().Name;
                    var factoryMethodName = sd.ImplementationFactory?.Method.Name;
                    var factoryDeclaringName = sd.ImplementationFactory?.Method.DeclaringType?.Name;

                    bool matches = removalKeywords.Any(k =>
                        (implTypeName != null && implTypeName.Contains(k, StringComparison.OrdinalIgnoreCase)) ||
                        (instTypeName != null && instTypeName.Contains(k, StringComparison.OrdinalIgnoreCase)) ||
                        (factoryMethodName != null && factoryMethodName.Contains(k, StringComparison.OrdinalIgnoreCase)) ||
                        (factoryDeclaringName != null && factoryDeclaringName.Contains(k, StringComparison.OrdinalIgnoreCase)));

                    if (matches)
                    {
                        services.Remove(sd);
                    }
                }

                services.AddSingleton<IRedisLeaderboardService, MockRedisLeaderboardService>();
                // Replace the ILeaderboardService with a mock to avoid DB access in tests
                services.AddSingleton<MathLearning.Application.Services.ILeaderboardService, MockLeaderboardService>();
                services.AddSingleton<MathLearning.Application.Services.ISchoolLeaderboardService, MockLeaderboardService>();
            });
    }
}

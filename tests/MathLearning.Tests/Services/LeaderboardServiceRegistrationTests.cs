using MathLearning.Api.Startup;
using MathLearning.Infrastructure.Services.Leaderboard;
using Microsoft.AspNetCore.Builder;

namespace MathLearning.Tests.Services;

public sealed class LeaderboardServiceRegistrationTests
{
    [Fact]
    public void ApplicationLayerDoesNotRegisterSecondSchoolAggregationOwner()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Test"
        });

        builder.AddApplicationLayerServices();

        Assert.DoesNotContain(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(SchoolLeaderboardAggregationService));
    }
}

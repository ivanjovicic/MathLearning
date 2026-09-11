using MathLearning.Api;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class QuestionEndpointsAbsenceTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> factory;

    public QuestionEndpointsAbsenceTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void DeadQuestionEndpointsType_IsRemovedFromApiAssembly()
    {
        var apiAssembly = typeof(Program).Assembly;
        var deadType = apiAssembly.GetType("MathLearning.Api.Endpoints.QuestionEndpoints");
        Assert.Null(deadType);

        Assert.DoesNotContain(
            apiAssembly.GetTypes(),
            type => string.Equals(type.Name, "QuestionEndpoints", StringComparison.Ordinal));
    }

    [Fact]
    public void Program_DoesNotRegisterMapQuestionEndpoints()
    {
        var programPath = Path.Combine(FindRepoRoot(), "src", "MathLearning.Api", "Program.cs");
        Assert.True(File.Exists(programPath), programPath);
        var source = File.ReadAllText(programPath);
        Assert.DoesNotContain("MapQuestionEndpoints", source, StringComparison.Ordinal);
        Assert.Contains("MapQuestionAuthoringEndpoints", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EndpointDataSource_HasNoLegacyGetQuestionsRouteNames()
    {
        using var scope = factory.Services.CreateScope();
        var dataSources = scope.ServiceProvider.GetServices<EndpointDataSource>();
        var names = dataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("GetQuestions", names);
        Assert.DoesNotContain("GetQuestion", names);
    }

    [Fact]
    public async Task LearnerGetApiQuestions_DoesNotExposeUnboundedLegacyList()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", "learner-1");

        var response = await client.GetAsync("/api/questions?limit=999999");

        // Canonical surface is authoring-only; the dead learner list endpoint must not be present.
        Assert.True(
            response.StatusCode is System.Net.HttpStatusCode.NotFound
                or System.Net.HttpStatusCode.MethodNotAllowed
                or System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.Forbidden,
            $"Unexpected status {response.StatusCode}");
        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MathLearning.slnx"))
                || File.Exists(Path.Combine(dir.FullName, "MathLearning.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}

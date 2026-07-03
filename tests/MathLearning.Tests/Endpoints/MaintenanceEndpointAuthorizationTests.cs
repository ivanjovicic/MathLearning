using System.Net;
using MathLearning.Api;
using MathLearning.Application.Services;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class MaintenanceEndpointAuthorizationTests :
    IClassFixture<CustomWebApplicationFactory<Program>>
{
    private static readonly (HttpMethod Method, string Path)[] MaintenanceRoutes =
    {
        (HttpMethod.Post, "/api/maintenance/rebuild-indexes"),
        (HttpMethod.Get, "/api/maintenance/index-health"),
        (HttpMethod.Get, "/api/maintenance/index-stats")
    };

    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public MaintenanceEndpointAuthorizationTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task AnonymousUser_CannotAccessAnyMaintenanceRoute()
    {
        foreach (var (method, path) in MaintenanceRoutes)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

            var response = await client.SendAsync(request);
            AssertUnauthorizedOrForbidden(path, response.StatusCode);
        }
    }

    [Fact]
    public async Task AuthenticatedLearner_CannotAccessAnyMaintenanceRoute()
    {
        foreach (var (method, path) in MaintenanceRoutes)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add("X-Test-UserId", "maintenance-learner");

            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public void EveryMaintenanceRoute_RequiresExplicitAdminPolicyMetadata()
    {
        _ = client;
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        var maintenanceEndpoints = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "/api/maintenance",
                StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        Assert.Equal(MaintenanceRoutes.Length, maintenanceEndpoints.Count);
        foreach (var endpoint in maintenanceEndpoints)
        {
            var authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            Assert.Contains(authorizeData, data =>
                string.Equals(
                    data.Policy,
                    DesignTokenSecurity.AdminPolicy,
                    StringComparison.Ordinal));
        }
    }

    private static void AssertUnauthorizedOrForbidden(string path, HttpStatusCode statusCode) =>
        Assert.True(
            statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected {path} to return 401/403, got {(int)statusCode}.");
}
